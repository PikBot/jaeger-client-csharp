namespace Jaeger.Core.Senders
{
    public interface ISender
    {
        int Append(Span span);

        int Flush();

        int Close();
    }
}