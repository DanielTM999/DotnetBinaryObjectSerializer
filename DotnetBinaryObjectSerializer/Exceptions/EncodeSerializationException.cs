namespace DotnetBinaryObjectSerializer.Exceptions
{
    public class EncodeSerializationException : SerializationException
    {
        public EncodeSerializationException(string message): base(message){} 

        public EncodeSerializationException(string message, Exception? innerException): base(message, innerException){} 
        
        public EncodeSerializationException(Exception? innerException): base(innerException){} 
    }
}
