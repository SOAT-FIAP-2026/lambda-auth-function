# ==============================================================================
# Variáveis — Lambda Auth Function
# ==============================================================================

variable "project_name" {
  description = "Nome do projeto — usado como prefixo dos recursos"
  type        = string
  default     = "techchallenge"
}

variable "environment" {
  description = "Ambiente de destino (dev, prod)"
  type        = string
  default     = "prod"
}

variable "aws_region" {
  description = "Região AWS. Deve ser a mesma da VPC e do RDS."
  type        = string
  default     = "sa-east-1"
}

variable "package_path" {
  description = "Caminho do .zip gerado por 'dotnet lambda package'"
  type        = string
  default     = "../lambda-auth.zip"
}

variable "timeout_seconds" {
  description = "Timeout da função em segundos"
  type        = number
  default     = 30
}

variable "memory_size" {
  description = "Memória da função em MB"
  type        = number
  default     = 512
}

variable "log_retention_days" {
  description = "Retenção dos logs no CloudWatch"
  type        = number
  default     = 7
}

# --- Rede ---------------------------------------------------------------------

variable "vpc_id" {
  description = "VPC criada em soat-infra. Vazio junto com subnet_ids publica a função fora da VPC."
  type        = string
  default     = ""
}

variable "subnet_ids" {
  description = "Subnets privadas com rota até o RDS. Lista vazia dispensa a configuração de VPC."
  type        = list(string)
  default     = []
}

# --- Segredos e configuração da função ----------------------------------------

variable "secret_arn" {
  description = "ARN do segredo no Secrets Manager com JWT_SECRET e a string de conexão. Vazio dispensa a policy de leitura."
  type        = string
  default     = ""
}

variable "db_connection_string" {
  description = "String de conexão Npgsql para o RDS. Injete via TF_VAR_db_connection_string; nunca commite o valor."
  type        = string
  sensitive   = true
}

variable "jwt_secret" {
  description = "Chave HMAC de assinatura do JWT, mínimo de 32 caracteres. Injete via TF_VAR_jwt_secret."
  type        = string
  sensitive   = true

  validation {
    condition     = length(var.jwt_secret) >= 32
    error_message = "jwt_secret precisa ter pelo menos 32 caracteres."
  }
}

variable "jwt_issuer" {
  description = "Claim iss do token emitido"
  type        = string
  default     = "fiap-tech-challenge"
}

variable "jwt_audience" {
  description = "Claim aud do token emitido"
  type        = string
  default     = "fiap-api"
}

variable "jwt_expires_in_seconds" {
  description = "Validade do token em segundos"
  type        = number
  default     = 3600
}

variable "cors_allow_origins" {
  description = "Origens aceitas pelo API Gateway"
  type        = list(string)
  default     = ["*"]
}
