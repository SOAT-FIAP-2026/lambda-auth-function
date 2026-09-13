# ==============================================================================
# Outputs — Lambda Auth Function
# ==============================================================================

output "function_name" {
  description = "Nome da função Lambda"
  value       = aws_lambda_function.auth.function_name
}

output "function_arn" {
  description = "ARN da função Lambda"
  value       = aws_lambda_function.auth.arn
}

output "auth_endpoint" {
  description = "URL completa do endpoint de autenticação — use como baseUrl na collection Postman"
  value       = "${aws_apigatewayv2_api.auth.api_endpoint}/auth/token"
}

output "api_endpoint" {
  description = "Endpoint base do API Gateway"
  value       = aws_apigatewayv2_api.auth.api_endpoint
}

output "log_group" {
  description = "Log group com os logs JSON estruturados da função"
  value       = aws_cloudwatch_log_group.lambda.name
}
