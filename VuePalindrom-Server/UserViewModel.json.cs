using Starcounter;
using Starcounter.Startup.Routing;

namespace VuePalindrom_Server
{
    [Url("/VuePalindrom-Server/user")]
    partial class UserViewModel : Json
    {
        
        [UserViewModel_json.User]
        partial class UserVM : Json
        {
            public string FullName => $"{FirstName} {LastName}";

            public void Handle(Input.FirstName update)
            {

            }

            public void Handle(Input.LastName update)
            {

            }
        }
        
    }
}
