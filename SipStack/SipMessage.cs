namespace SipStack
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;

    using SipStack.Isup;

    public class SipMessage
    {
        private const string BoundaryId = "unique-boundary-1";

        protected SipMessage()
        {
            this.Headers = new OrderedDictionary();
        }

        protected SipMessage(Contact to, string method)
            : this(method)
        {
            this.Headers["To"] = to;
        }

        public SipMessage(string method)
            : this()
        {
            this.Method = method;
            this.Headers["Allow"] = "INVITE, ACK, PRACK, CANCEL, BYE, OPTIONS, MESSAGE, NOTIFY, UPDATE, REGISTER, INFO, REFER, SUBSCRIBE, PUBLISH";
        }

        public Contact To
        {
            get
            {
                var h = this.Headers["To"];
                if (h is string)
                {
                    return (Contact)(this.Headers["To"] = Contact.Parse(h.ToString()));
                }

                return (Contact)h;
            }
        }

        public Sdp SdpData { get; set; }

        public IsupBody IsupData { get; set; }

        public Contact From
        {
            get
            {
                var h = this.Headers["From"];
                if (h is string)
                {
                    return (Contact)(this.Headers["From"] = Contact.Parse(h.ToString()));
                }

                return (Contact)h;
            }
        }

        public Contact Contact
        {
            get
            {
                var h = this.Headers["Contact"];
                if (h is string)
                {
                    return (Contact)(this.Headers["Contact"] = Contact.Parse(h.ToString()));
                }

                return (Contact)h;
            }
        }

        public string Method { get; protected set; }

        public OrderedDictionary Headers { get; private set; }

        public static SipMessage Parse(byte[] buffer)
        {
            var cmd = Encoding.Default.GetString(buffer.TakeWhile(a => a != ' ').ToArray()).ToLowerInvariant();
            switch (cmd)
            {
                case "invite":
                    return new InviteMessage(buffer);
                case "sip/2.0":
                    int responseCode;

                    var str = string.Concat(buffer.Skip(cmd.Length + 1).Take(3).ToArray());

                    if (int.TryParse(str, out responseCode))
                    {
                        return new SipResponse(buffer);
                    }
                    throw new InvalidOperationException("response code not understood: " + str);
                default:
                    return Parse(buffer);
            }
        }

        public virtual byte[] Serialize()
        {
            using (var ms = new MemoryStream())
            {
                var sb = new StreamWriter(ms, Encoding.Default);

                sb.WriteLine("{0} {1} SIP/2.0", this.Method, this.To.ToString(false));

                var bodies = this.GetBodies();
                var bodyBuilder = new StringBuilder();
                if (bodies.Length > 0)
                {
                    if (bodies.Length == 1)
                    {
                        this.Headers["Content-Type"] = bodies[0].ContentType;
                    }
                    else
                    {
                        this.Headers["Content-Type"] = string.Format("multipart/mixed;boundary={0}", BoundaryId);
                    }

                    if (bodies.Length > 1)
                    {
                        bodyBuilder.AppendLine();
                        bodyBuilder.AppendLine(string.Format("--{0}", BoundaryId));
                    }

                    foreach (var b in bodies)
                    {
                        if (bodies.Length > 1)
                        {
                            bodyBuilder.AppendLine(string.Format("Content-Type: {0}", b.ContentType));
                        }

                        foreach (var kvp in b.Headers)
                        {
                            bodyBuilder.AppendLine(string.Format("{0}: {1}", kvp.Key, kvp.Value));
                        }

                        bodyBuilder.AppendLine();

                        bodyBuilder.AppendLine(b.ContentText);
                        if (bodies.Length > 1)
                        {
                            bodyBuilder.AppendLine(string.Format("--{0}", BoundaryId));
                        }
                    }

                    bodyBuilder.Length = bodyBuilder.Length - 2;
                    if (bodies.Length > 1)
                    {
                        bodyBuilder.AppendLine("--");
                    }

                    // content length does not counts on last 2 digits
                    this.Headers["Content-Length"] = (bodyBuilder.Length - 2).ToString(CultureInfo.InvariantCulture);
                }


                foreach (DictionaryEntry kvp in this.Headers)
                {
                    sb.WriteLine("{0}: {1}", kvp.Key, kvp.Value);
                }

                if (bodyBuilder.Length > 0)
                {
                    sb.Write(bodyBuilder);
                }
                else
                {
                    sb.WriteLine();
                }

                sb.Flush();
                return ms.ToArray();
            }
        }

        protected void ParseBuffer(byte[] buffer)
        {
            var bs = new ByteStream(buffer, 0);
            this.ParseRequestLine(bs.ReadLine());
            var bodies = new List<Body>();

            foreach (var l in bs.Lines())
            {
                if (l == string.Empty && this.Headers.Contains("Content-Length") && this.Headers["Content-Length"].ToString() != "0")
                {
                    bodies.AddRange(SipResponse.BodyParser.Parse(this.Headers["Content-Type"].ToString(), bs.Read(bs.Length - bs.Position)).ToList());
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(l))
                    {
                        this.ParseHeader(l);
                    }
                }
            }

            this.SdpData = bodies.OfType<Sdp>().FirstOrDefault();
            this.IsupData = bodies.OfType<IsupBody>().FirstOrDefault();
        }

        protected virtual Body[] GetBodies()
        {
            if (this.IsupData != null && this.SdpData != null)
            {
                return new Body[] { this.SdpData, this.IsupData };
            }

            if (this.SdpData != null)
            {
                return new Body[] { this.SdpData };
            }

            return this.IsupData != null ? new Body[] { this.IsupData } : new Body[0];
        }

        protected virtual void ParseRequestLine(string line)
        {
            this.Method = line.Substring(0, line.IndexOf(' '));
            this.Headers["To"] = Contact.Parse(line.Substring(line.IndexOf(' ') + 1, line.Length - line.IndexOf(' ') - 9));
        }

        protected void ParseHeader(string line)
        {
            var headerName = line.Substring(0, line.IndexOf(':'));
            var headerValue = line.Substring(line.IndexOf(':') + 1).TrimStart(' ');
            this.Headers[headerName] = headerValue;
        }
    }
}