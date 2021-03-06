namespace SipStack
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    public class Contact
    {
        private readonly string protocol;

        private readonly string name;

        private readonly string address;

        private readonly List<KeyValuePair<string, string>> parameters;
        private readonly List<KeyValuePair<string, string>> trailing;

        public Contact(string address, string name = null, KeyValuePair<string, string>[] parameters = null, KeyValuePair<string, string>[] trailingParameters = null)
        {
            this.protocol = null;
            this.name = name;
            this.address = address;

            this.parameters = new List<KeyValuePair<string, string>>(parameters != null && parameters.Length > 0 ? new List<KeyValuePair<string, string>>(parameters) : Enumerable.Empty<KeyValuePair<string, string>>());
            this.trailing = new List<KeyValuePair<string, string>>(trailingParameters != null && trailingParameters.Length > 0 ? new List<KeyValuePair<string, string>>(trailingParameters) : Enumerable.Empty<KeyValuePair<string, string>>());

            if (this.address.Contains(':'))
            {
                var idx = this.address.IndexOf(':');

                // guess if its a port or a protocol
                var portString = this.address.Substring(idx + 1);
                int port;
                var isPort = int.TryParse(portString, out port);
                if (!isPort)
                {
                    this.protocol = this.address.Substring(0, idx);
                    this.address = this.address.Substring(idx + 1);
                }
            }
        }

        public string Name
        {
            get
            {
                return this.name;
            }
        }

        public string Address
        {
            get
            {
                return this.address;
            }
        }

        public string Protocol
        {
            get
            {
                return this.protocol;
            }
        }

        public static implicit operator Contact(string input)
        {
            return Parse(input);
        }

        public static Contact Parse(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                throw new ArgumentNullException("input");
            }

            input = input.TrimStart();
            var isQuoted = input.StartsWith("\"");

            string name = null;
            string address;

            var idx = input.IndexOf(isQuoted ? '"' : ' ', isQuoted ? 1 : 0);
            if (idx > -1)
            {
                name = input.Substring(0, idx + (isQuoted ? 1 : 0));
                input = input.Substring(idx + 1);
            }

            var parameters = new List<KeyValuePair<string, string>>();
            var trailing = new List<KeyValuePair<string, string>>();

            if (isQuoted)
            {
                input = input.Substring(input.IndexOf('"', 1) + 1);
            }
            
            if (input.IndexOf('<') > -1 && input.IndexOf('>') > -1)
            {
                var trailingContent = input.Substring(input.IndexOf('>'));
                var kvp = from x in trailingContent.Split(';')
                          let split = x.Split('=')
                          where split.Length == 2
                          select new KeyValuePair<string, string>(split[0], split[1]);
                trailing = kvp.ToList();

                input = input.Substring(input.IndexOf('<') + 1, input.LastIndexOf('>') - 1);
            }

            idx = input.IndexOf(';');
            if (idx == -1)
            {
                address = input.Trim();
            }
            else
            {
                address = input.Substring(0, idx).Trim();

                input = input.Substring(idx + 1);

                var kvp = from x in input.Split(';')
                          let split = x.Split('=')
                          where split.Length == 2
                          select new KeyValuePair<string, string>(split[0], split[1]);
                parameters = kvp.ToList();
            }

            return new Contact(address, name, parameters.ToArray(), trailing.ToArray());
        }

        public override bool Equals(object obj)
        {
            if (obj is string)
            {
                var str = obj as string;
                return this.ToString(str.Contains("<") || str.Contains(">")).Equals(obj.ToString());
            }

            return obj is Contact && (obj as Contact).ToString() == this.ToString();
        }

        public override int GetHashCode()
        {
            return this.ToString(true).GetHashCode();
        }

        public string ToString(bool includeQuotes)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(this.name))
            {
                sb.AppendFormat("{0} ", this.name);
            }

            if (includeQuotes)
            {
                sb.Append("<");
            }

            sb.AppendFormat("sip:{0}", this.address);
            if (this.parameters.Count > 0)
            {
                sb.Append(";");
                sb.Append(string.Join(";", this.parameters.Select(a => string.Format("{0}={1}", a.Key, a.Value))));
            }

            if (includeQuotes)
            {
                sb.Append(">");
            }

            if (this.trailing.Count > 0)
            {
                sb.Append(";");
                sb.Append(string.Join(";", this.trailing.Select(a => string.Format("{0}={1}", a.Key, a.Value))));
            }
            
            return sb.ToString();
        }

        public override string ToString()
        {
            return this.ToString(true);
        }

        public IEnumerable<KeyValuePair<string, string>> GetParameters()
        {
            return this.parameters;
        }
    }
}