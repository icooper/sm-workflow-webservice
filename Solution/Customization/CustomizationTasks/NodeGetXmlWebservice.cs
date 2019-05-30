/*
 * NodeGetXmlWebService.cs
 * 
 * Workflow node to perform a GET request on a webservice that returns XML.
 * 
 * Ian Cooper
 * 30 May 2019
 *
 */

using System;
using System.Xml;
using System.Xml.XPath;

using Thermo.SampleManager.Server.Workflow.Attributes;
using Thermo.SampleManager.Server.Workflow.Definition;

namespace Thermo.SampleManager.Server.Workflow.Nodes
{

    // workflow node attributes
    [WorkflowNode(
        NodeType,
        "NodeGetXmlWebserviceName",
        "NodeCategoryWebservices",
        "GEAR",
        "NodeGetXmlWebserviceDescription",
        MessageGroup)]
    [Tag(WorkflowNodeTypeInternal.TagOutput)]
    [FollowsTag(WorkflowNodeTypeInternal.TagData)]

    class NodeGetXmlWebservice : Node
    {

        #region Constants

        public const string NodeType = "NODE_GETXMLWS";
        public const string MessageGroup = "WebserviceWorkflowMessages";
        public const string ParameterUrl = "ADDRESS";
        public const string ParameterQuery = "QUERY";
        public const string ParameterNamespacePrefix = "NSPREFIX";
        public const string ParameterXpath = "XPATH";
        public const string ParameterVariableName = "VARIABLENAME";

        #endregion

        #region Parameters

        [Parameter(
            ParameterUrl,
            "NodeGetXmlWebserviceAddrName",
            "NodeGetXmlWebserviceAddrDesc",
            MessageGroup = MessageGroup,
            Mandatory = true)]
        public string RequestAddress
        {
            get
            {
                string value = this.GetParameterBagValue(ParameterUrl, "");
                return string.IsNullOrWhiteSpace(value) ? "" : value;
            }

            set
            {
                // remove any trailing '?'
                this.SetParameterBagValue(ParameterUrl, string.IsNullOrWhiteSpace(value) ? "" : value.TrimEnd('?'));
            }
        }

        [FormulaParameter(
            ParameterQuery,
            "NodeGetXmlWebserviceQueryStringName",
            "NodeGetXmlWebserviceQueryStringDesc",
            MessageGroup = MessageGroup,
            Mandatory = false)]
        public string QueryString
        {
            get
            {
                string value = this.GetParameterBagValue(ParameterQuery, "");
                return string.IsNullOrWhiteSpace(value) ? "" : value;
            }

            set
            {
                this.SetParameterBagValue(ParameterQuery, string.IsNullOrWhiteSpace(value) ? "" : value);
            }
        }

        [Parameter(
            ParameterNamespacePrefix,
            "NodeGetXmlWebserviceNsPrefixName",
            "NodeGetXmlWebserviceNsPrefixDesc",
            MessageGroup = MessageGroup,
            Mandatory = false)]
        public string NamespacePrefix
        {
            get
            {
                string value = this.GetParameterBagValue(ParameterNamespacePrefix, "");
                return string.IsNullOrWhiteSpace(value) ? "" : value;
            }

            set
            {
                this.SetParameterBagValue(ParameterNamespacePrefix, string.IsNullOrWhiteSpace(value) ? "" : value);
            }
        }

        [Parameter(
            ParameterXpath,
            "NodeGetXmlWebserviceResponseQueryName",
            "NodeGetXmlWebserviceResponseQueryDesc",
            MessageGroup = MessageGroup,
            Mandatory = true)]
        public string XpathQuery
        {
            get
            {
                string value = this.GetParameterBagValue(ParameterXpath, "");
                return string.IsNullOrWhiteSpace(value) ? "" : value;
            }

            set
            {
                this.SetParameterBagValue(ParameterXpath, string.IsNullOrWhiteSpace(value) ? "" : value);
            }
        }

        [FormulaParameter(
            ParameterVariableName,
            "NodeGetXmlWebserviceDestVariableName",
            "NodeGetXmlWebserviceDestVariableDesc",
            MessageGroup = MessageGroup,
            Mandatory = true)]
        public string VariableName
        {
            get
            {
                string value = this.GetParameterBagValue(ParameterVariableName, "");
                return string.IsNullOrWhiteSpace(value) ? "" : value;
            }

            set
            {
                this.SetParameterBagValue(ParameterVariableName, string.IsNullOrWhiteSpace(value) ? "" : value);
            }
        }

        #endregion

        #region Constructor

        public NodeGetXmlWebservice(WorkflowNodeInternal node) : base(node)
        {
            // nothing
        }

        #endregion

        #region Methods

        public override string AutoName()
        {
            string name = GetMessage("NodeGetXmlWebserviceNameBare");

            if (!string.IsNullOrWhiteSpace(RequestAddress))
            {
                try
                {
                    name = GetMessage("NodeGetXmlWebserviceNameFormat", new Uri(this.RequestAddress).Host);
                }
                catch (UriFormatException)
                {
                    // nothing
                }
            }

            return name;
        }

        public override bool PerformNode()
        {
            this.TracePerformNode();
            string variableName = GetFormulaText(VariableName),
                address = RequestAddress,
                query = GetFormulaText(QueryString),
                xpath = XpathQuery;

            // validate variable name
            if (string.IsNullOrWhiteSpace(variableName))
            {
                AddErrorMessage("ErrorInvalidVariableNameFormat");
                return false;
            }

            // validate URI format
            string url = string.IsNullOrWhiteSpace(query) ? address : $"{address}?{query.TrimStart('?')}";
            try
            {
                url = new Uri(url).ToString();
                Properties.SetGlobalVariable($"DEBUG_{NodeType}_URL", url);
            }
            catch (UriFormatException)
            {
                AddErrorMessage("ErrorUriFormatExceptionFormat", url);
                return false;
            }

            // query the webservice
            XmlDocument doc = new XmlDocument();
            try
            {
                doc.Load(new XmlTextReader(url));
            }
            catch (Exception e)
            {
                AddErrorMessage("ErrorRequestExceptionFormat", e.Message);
                return false;
            }

            // set up the default namespace binding
            XmlNamespaceManager nsmgr = new XmlNamespaceManager(doc.NameTable);
            if (!string.IsNullOrWhiteSpace(NamespacePrefix))
            {
                nsmgr.AddNamespace(NamespacePrefix, doc.DocumentElement.NamespaceURI);
            }

            // extract the output
            string result = null;
            try
            {
                XmlNode node = doc.DocumentElement.SelectSingleNode(xpath, nsmgr);
                if (node != null)
                {
                    switch (node.NodeType)
                    {
                        case XmlNodeType.Attribute:
                            result = node.Value;
                            break;

                        case XmlNodeType.Element:
                            result = node.InnerText;
                            break;

                        default:
                            AddErrorMessage("ErrorUnknownNodeTypeFormat", node.NodeType.ToString());
                            return false;
                            // break;
                    }
                }
            }
            catch (XPathException e)
            {
                AddErrorMessage("ErrorXPathExceptionFormat", e.Message);
                return false;
            }

            // set the workflow variable
            Properties.SetGlobalVariable(variableName, result);

            return true;
        }

        protected override string GetMessage(string message)
        {
            return Library.Message.GetMessage(MessageGroup, message);
        }

        protected override string GetMessage(string message, params object[] parameters)
        {
            return Library.Message.GetMessage(MessageGroup, message, parameters);
        }

        #endregion

    }
}
