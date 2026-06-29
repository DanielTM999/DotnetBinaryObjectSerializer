namespace DotnetBinaryObjectSerializer.Exceptions
{
    public class DecodeSerializationException : SerializationException
    {
        public DecodeSerializationException(string message): base(message){} 

        public DecodeSerializationException(string message, Exception? innerException): base(message, innerException){} 
        
        public DecodeSerializationException(Exception? innerException): base(innerException){} 
    }
}
