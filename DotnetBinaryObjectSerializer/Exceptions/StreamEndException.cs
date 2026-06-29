namespace DotnetBinaryObjectSerializer.Exceptions
{
    public class StreamEndException : DecodeSerializationException
    {
        public StreamEndException(string message): base(message){} 

        public StreamEndException(string message, Exception? innerException): base(message, innerException){} 
        
        public StreamEndException(Exception? innerException): base(innerException){}
    }
}
