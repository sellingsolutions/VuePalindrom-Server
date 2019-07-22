using Starcounter;

namespace VuePalindrom_Server
{
    partial class UserViewModel : Json
    {
        protected void Handle(Input.ResetNameClicked action)
        {
            action.Cancel();

            this.User.FirstName = "Isaac";
            this.User.LastName = "Newton";
        }

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