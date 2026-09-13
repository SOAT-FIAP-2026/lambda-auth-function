terraform {
  required_version = ">= 1.5"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }

  # Backend remoto — habilite quando o bucket de estado estiver disponível.
  # backend "s3" {
  #   bucket = "fiap-soat-terraform-state"
  #   key    = "lambda-auth/terraform.tfstate"
  #   region = "us-east-1"
  # }
}

provider "aws" {
  region = var.aws_region
}
