# ==============================================================================
# Lambda Auth Function — Infraestrutura
# ==============================================================================
# Provisiona a função de autenticação por CPF e a expõe pelo API Gateway HTTP:
#
#   POST {api_endpoint}/auth/token  ->  Lambda  ->  RDS PostgreSQL
#
# A função roda nas subnets privadas da VPC criada em soat-infra para alcançar o
# RDS de soat-db. Segredos (JWT_SECRET e string de conexão) vêm do Secrets
# Manager quando secret_arn é informado, e nunca são versionados.
# ==============================================================================

locals {
  function_name = "${var.project_name}-${var.environment}-auth"

  tags = {
    Project     = var.project_name
    Environment = var.environment
    ManagedBy   = "terraform"
  }
}

# --- Role de execução ---------------------------------------------------------
data "aws_iam_policy_document" "lambda_assume" {
  statement {
    actions = ["sts:AssumeRole"]

    principals {
      type        = "Service"
      identifiers = ["lambda.amazonaws.com"]
    }
  }
}

resource "aws_iam_role" "lambda" {
  name               = "${local.function_name}-role"
  assume_role_policy = data.aws_iam_policy_document.lambda_assume.json
  tags               = local.tags
}

# Logs no CloudWatch — necessário para os logs JSON estruturados da função
resource "aws_iam_role_policy_attachment" "lambda_basic" {
  role       = aws_iam_role.lambda.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole"
}

# ENIs nas subnets privadas — exigido para alcançar o RDS dentro da VPC
resource "aws_iam_role_policy_attachment" "lambda_vpc" {
  count      = length(var.subnet_ids) > 0 ? 1 : 0
  role       = aws_iam_role.lambda.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSLambdaVPCAccessExecutionRole"
}

# Leitura do segredo com JWT_SECRET e string de conexão
data "aws_iam_policy_document" "secrets" {
  count = var.secret_arn == "" ? 0 : 1

  statement {
    actions   = ["secretsmanager:GetSecretValue"]
    resources = [var.secret_arn]
  }
}

resource "aws_iam_role_policy" "secrets" {
  count  = var.secret_arn == "" ? 0 : 1
  name   = "${local.function_name}-secrets"
  role   = aws_iam_role.lambda.id
  policy = data.aws_iam_policy_document.secrets[0].json
}

# --- Security Group da função -------------------------------------------------
resource "aws_security_group" "lambda" {
  count       = length(var.subnet_ids) > 0 ? 1 : 0
  name        = "${local.function_name}-sg"
  description = "Egress da Lambda de autenticacao para o RDS PostgreSQL"
  vpc_id      = var.vpc_id

  egress {
    description = "All outbound"
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = merge(local.tags, { Name = "${local.function_name}-sg" })
}

# --- Log group ----------------------------------------------------------------
resource "aws_cloudwatch_log_group" "lambda" {
  name              = "/aws/lambda/${local.function_name}"
  retention_in_days = var.log_retention_days
  tags              = local.tags
}

# --- Função -------------------------------------------------------------------
resource "aws_lambda_function" "auth" {
  function_name = local.function_name
  role          = aws_iam_role.lambda.arn
  runtime       = "dotnet8"
  handler       = "Fiap.TechChallenge.LambdaAuth::Fiap.TechChallenge.LambdaAuth.Function::HandleAsync"

  filename         = var.package_path
  source_code_hash = filebase64sha256(var.package_path)

  timeout     = var.timeout_seconds
  memory_size = var.memory_size

  environment {
    variables = {
      DB_CONNECTION_STRING   = var.db_connection_string
      JWT_SECRET             = var.jwt_secret
      JWT_ISSUER             = var.jwt_issuer
      JWT_AUDIENCE           = var.jwt_audience
      JWT_EXPIRES_IN_SECONDS = tostring(var.jwt_expires_in_seconds)
    }
  }

  dynamic "vpc_config" {
    for_each = length(var.subnet_ids) > 0 ? [1] : []

    content {
      subnet_ids         = var.subnet_ids
      security_group_ids = [aws_security_group.lambda[0].id]
    }
  }

  depends_on = [
    aws_iam_role_policy_attachment.lambda_basic,
    aws_cloudwatch_log_group.lambda,
  ]

  tags = local.tags
}

# --- API Gateway HTTP ---------------------------------------------------------
resource "aws_apigatewayv2_api" "auth" {
  name          = "${local.function_name}-api"
  protocol_type = "HTTP"
  description   = "Autenticacao por CPF do Tech Challenge"

  cors_configuration {
    allow_origins = var.cors_allow_origins
    allow_methods = ["POST", "OPTIONS"]
    allow_headers = ["content-type", "x-correlation-id"]
  }

  tags = local.tags
}

resource "aws_apigatewayv2_integration" "auth" {
  api_id                 = aws_apigatewayv2_api.auth.id
  integration_type       = "AWS_PROXY"
  integration_uri        = aws_lambda_function.auth.invoke_arn
  payload_format_version = "1.0" # a funcao usa APIGatewayProxyRequest/Response
}

resource "aws_apigatewayv2_route" "auth" {
  api_id    = aws_apigatewayv2_api.auth.id
  route_key = "POST /auth/token"
  target    = "integrations/${aws_apigatewayv2_integration.auth.id}"
}

resource "aws_cloudwatch_log_group" "api" {
  name              = "/aws/apigateway/${local.function_name}"
  retention_in_days = var.log_retention_days
  tags              = local.tags
}

resource "aws_apigatewayv2_stage" "default" {
  api_id      = aws_apigatewayv2_api.auth.id
  name        = "$default"
  auto_deploy = true

  access_log_settings {
    destination_arn = aws_cloudwatch_log_group.api.arn
    format = jsonencode({
      requestId      = "$context.requestId"
      correlation_id = "$context.requestId"
      httpMethod     = "$context.httpMethod"
      routeKey       = "$context.routeKey"
      status         = "$context.status"
      responseLength = "$context.responseLength"
      latency_ms     = "$context.responseLatency"
    })
  }

  tags = local.tags
}

resource "aws_lambda_permission" "api_gateway" {
  statement_id  = "AllowExecutionFromAPIGateway"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.auth.function_name
  principal     = "apigateway.amazonaws.com"
  source_arn    = "${aws_apigatewayv2_api.auth.execution_arn}/*/*"
}
