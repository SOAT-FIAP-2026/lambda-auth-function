using System.Text.RegularExpressions;
using Fiap.TechChallenge.LambdaAuth.Exceptions;

namespace Fiap.TechChallenge.LambdaAuth.Services;

public class CpfValidatorService
{
    private static readonly Regex ApenasDigitos = new(@"\D", RegexOptions.Compiled);

    public string ValidarENormalizar(string cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf))
            throw new CpfInvalidoException("CPF não pode ser vazio.");

        var digitos = ApenasDigitos.Replace(cpf, "");

        if (digitos.Length != 11)
            throw new CpfInvalidoException("CPF deve conter 11 dígitos.");

        if (digitos.Distinct().Count() == 1)
            throw new CpfInvalidoException("CPF inválido: todos os dígitos são iguais.");

        if (!ValidarDigitosVerificadores(digitos))
            throw new CpfInvalidoException("CPF inválido: dígitos verificadores incorretos.");

        return digitos;
    }

    private static bool ValidarDigitosVerificadores(string digitos)
    {
        var d = digitos.Select(c => c - '0').ToArray();

        var soma1 = 0;
        for (var i = 0; i < 9; i++) soma1 += d[i] * (10 - i);
        var resto1 = soma1 % 11;
        var dv1 = resto1 < 2 ? 0 : 11 - resto1;
        if (d[9] != dv1) return false;

        var soma2 = 0;
        for (var i = 0; i < 10; i++) soma2 += d[i] * (11 - i);
        var resto2 = soma2 % 11;
        var dv2 = resto2 < 2 ? 0 : 11 - resto2;
        return d[10] == dv2;
    }
}
