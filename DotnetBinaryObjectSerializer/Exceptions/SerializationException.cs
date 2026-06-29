namespace DotnetBinaryObjectSerializer.Exceptions
{
    public class SerializationException : Exception
    {
        
        public SerializationException(string message): base(message){} 

        public SerializationException(string message, Exception? innerException): base(message, innerException){} 
        
        public SerializationException(Exception? innerException): base(null, innerException){}         
    }
}
