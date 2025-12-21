namespace LedgerCore.Api.Common.Exceptions
{
    public class NoUserDataException : ApiException
    {
        public NoUserDataException() : base("No user data found") 
        {
        }
    }
}
