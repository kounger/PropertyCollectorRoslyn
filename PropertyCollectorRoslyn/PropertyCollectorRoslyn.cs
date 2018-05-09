using System;
using System.Collections.Generic;
using System.Xml;
using System.Linq;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

/// <summary>
///This program extracts all properties from a SyntaxTree which was defined in the main method. 
///It uses Roslyn to find all Properties and searches through all classes of the Tree to find them. 
///The advantage of using Roslyn over Reflection for this task is that it can also extract the 
///XML-Summary of a property. The PropertyProcessor class includes methods to print all Properties, 
///to create a switch case statement to call every Property and a method to write all properties to 
///a csv file.
/// </summary>
namespace MetadataSwitchCaseGeneratorRoslyn
{
    public class Property
    {
        public string PropertyName { get; } = null;
        public string PropertyClassName { get; } = null;
        public string PropertyCall { get; } = null;
        public string PropertySummary { get; } = null;

        public Property(String propertyName, String propertyClassName, String propertyCall, String propertySummary)
        {
            this.PropertyName = propertyName;
            this.PropertyClassName = propertyClassName;
            this.PropertyCall = propertyCall;
            this.PropertySummary = propertySummary;
        }
    }

    public class PropertyWalker : CSharpSyntaxWalker
    {
        private List<String> classesToCallList = new List<string>();
        private String classNodeName;
        private String parentClassNodeName = null;

        public List<Property> PropertyKeyList { get; } = new List<Property>();

        /// <summary>
        ///Class Visitor
        /// </summary>
        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            classNodeName = node.Identifier.ToString();

            //We have to get the ParentClassNode to build a method call for a property
            if (node.Parent.Kind().ToString() == "NamespaceDeclaration")
            {
                ClassesToCallProperty(null, classNodeName);
            }
            else if (node.Parent.Kind().ToString() == "ClassDeclaration")
            {
                var parentClassNode = (ClassDeclarationSyntax)node.Parent;
                parentClassNodeName = parentClassNode.Identifier.ToString();                
                ClassesToCallProperty(parentClassNodeName, classNodeName);
            }

            base.VisitClassDeclaration(node);
        }

        /// <summary>
        ///Property Visitor
        /// </summary>
        public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            String propertyName = node.Identifier.ToString();
            String propertyCall = SequenceToCallProperty(classesToCallList, propertyName);
            String propertySummary = GetXMLSummary(node.GetLeadingTrivia());
            PropertyKeyList.Add(new Property(propertyName, classNodeName, propertyCall, propertySummary));

            base.VisitPropertyDeclaration(node);
        }

        /// <summary>
        ///Method maintains a list with class names in the order how they have to be called to get a Property.
        /// </summary>
        public void ClassesToCallProperty(String parentClassNodeName, String classNodeName)
        {
            if (!classesToCallList.Any())
            {
                classesToCallList.Add(parentClassNodeName);
            }
            
            //If the class parent node of a class node already exists in a list, then everything after this node
            //should be deleted from the list. After this a new class node can be added to the list.
            if (classesToCallList.Contains(parentClassNodeName))
            {
                int afterParentIndex = classesToCallList.IndexOf(parentClassNodeName) + 1;
                int range = classesToCallList.Count - afterParentIndex;
                classesToCallList.RemoveRange(afterParentIndex, range);
            }

            classesToCallList.Add(classNodeName);
        }

        /// <summary>
        ///Method returns a String which resembles the sequence to call a property. 
        /// </summary>
        private String SequenceToCallProperty(List<String> classesToCallList, String propertyName)
        {
            String returnString = null;

            foreach (String classToCall in classesToCallList)
            {
                if (returnString == null)
                {
                    returnString = classToCall;
                }
                else
                {
                    returnString = returnString + "." + classToCall;
                }
            }
            return returnString + "." + propertyName;
        }

        /// <summary>
        ///Method to extract the Summary from the XML Documentation of a Property inside the SyntaxTree.
        ///To get the summary every Property must have an XML-Documentation of the form: &lt;summary&gt;&lt;/summary&gt;
        /// </summary>
        private String GetXMLSummary(SyntaxTriviaList trivias)
        {
            //Get XML-Trivia from Node
            var xmlCommentTrivia = trivias.FirstOrDefault(t => t.Kind() == SyntaxKind.SingleLineDocumentationCommentTrivia);
            var xml = xmlCommentTrivia.GetStructure();
            if(xml == null){return "";}
            String xmlString = xml.ToString();
            //Get summary from XML-String
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xmlString);
            XmlNodeList xnList = xmlDoc.DocumentElement.SelectNodes("/summary");
            String summary = xnList.Item(0).InnerText;
            //Edit Summary-String            
            summary = summary.Replace("///", "");
            summary = summary.Trim();            

            return summary;
        }
    }

    public class PropertyProcessor
    {
        private List<Property> properties = null;

        public PropertyProcessor(List<Property> properties)
        {
            this.properties = properties;
        }

        public void PrintProperties()
        {
            foreach (Property property in properties)
            {
                Console.WriteLine(property.PropertyClassName + "\t" + property.PropertyName + "\t" + property.PropertyCall);
                Console.WriteLine(property.PropertySummary + "\n");                
            }
        }

        ///<summary>
        ///This method generates a custom switch case statement to call every property we collected and saves it in a file. 
        ///Edit this method to generate a switch case statement of your liking.
        ///</summary>
        //The switch case statement will have following form:
        /*
        switch (expression)
            {                    
            case "ClassName.ClassName.PropertyName":
                result = object.ClassName.ClassName.PropertyName;
                break;

            [...]

            default:
                result = null;                        
                break;
            }
        */
        public void SwitchCaseGenerator(String saveFolderPath)
        {
            String fourTabs = new String('\t', 4);
            String fiveTabs = new String('\t', 5);
            String sixTabs = new String('\t', 6);

            using (StreamWriter outputFile = new StreamWriter(saveFolderPath + @"\SwitchCaseRoslyn.txt"))
            {
                outputFile.WriteLine(fourTabs + "switch (expression)");
                outputFile.WriteLine(fiveTabs + "{");

                foreach (Property property in properties)
                {
                    outputFile.WriteLine(fiveTabs + "case " + @"""" + property.PropertyCall + @""":");
                    outputFile.WriteLine(sixTabs + "result = object." + property.PropertyCall + ";");
                    outputFile.WriteLine(sixTabs + "break;");
                    outputFile.WriteLine();
                }

                outputFile.WriteLine(fiveTabs + "default:");
                outputFile.WriteLine(sixTabs + "result = null;");
                outputFile.WriteLine(sixTabs + "break;");
                outputFile.WriteLine(fiveTabs + "}");
            }
        }

        /// <summary>
        ///This method creates a csv file which has a line for every property we collected.
        ///Every line will have the class of the property, its name, and it's summary. 
        /// </summary>
        public void CsvPropertyListGenerator(String saveFolderPath)
        {
            using (StreamWriter outputFile = new StreamWriter(saveFolderPath + @"\PropertiesRoslyn.csv"))
            {
                outputFile.WriteLine("sep=,"); // make Excel use comma as field separator
                foreach (Property property in properties)
                {
                    outputFile.WriteLine("{0},{1},{2},{3}", property.PropertyClassName, property.PropertyName, property.PropertyCall, property.PropertySummary);
                }
            }
        }
    }

    class PropertyCollectorRoslyn
    {
        static void Main(string[] args)
        {
            
            //Read SystemProperties.cs file
            SyntaxTree tree;
                        
            //Path to test file SyntaxTreeTest.cs
            String testPath = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.FullName + @"\SyntaxTreeTest.cs";
            
            //Path to the .cs file whose properties we want to extract:              
            /////////////EDIT THIS/////////////
            var path = testPath;
            ///////////////////////////////////

            using (var stream = File.OpenRead(path))
            {
                tree = CSharpSyntaxTree.ParseText(SourceText.From(stream), path: path);
            }

            //root node to start
            var root = tree.GetRoot();

            //Start collecting Properties from a certain class inside the SyntaxTree instead: 
            //String startClassName = "Interior";
            //var root = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Where(n => n.Identifier.ValueText == startClassName).First();

            //Collecting all properties:
            var walker = new PropertyWalker();
            walker.Visit(root);

            //Processing all properties we collected:
            var properties = new PropertyProcessor(walker.PropertyKeyList);

            //Print all properties with description if available 
            properties.PrintProperties();

            //Create a custom switch case statement which allows us to call every property
            String desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            properties.SwitchCaseGenerator(desktopPath);

            //Create a csv file which contains a list of all properties with a description (if available)
            properties.CsvPropertyListGenerator(desktopPath);
        }
    }
}
