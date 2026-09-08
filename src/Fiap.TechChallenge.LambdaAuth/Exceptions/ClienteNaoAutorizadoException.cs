namespace Fiap.TechChallenge.LambdaAuth.Exceptions;

public class ClienteNaoAutorizadoException : Exception
{
    public ClienteNaoAutorizadoException(string mensagem) : base(mensagem) { }
}
