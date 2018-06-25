using System;
using System.Collections.Generic;
using System.Xml;
using System.Linq;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PropertyCollectorRoslyn
{
    public class Property
    {
        public string PropertyName { get; } = null;
        public string PropertyClassName { get; } = null;
        public string PropertyCanonicalName { get; } = null; //sequence to call property as string
        public string PropertyTypeName { get; } = null;
        public string PropertySummary { get; } = null;        

        public Property(string propertyName, string propertyClassName, string propertyCanonicalName, string PropertyTypeName, string propertySummary)
        {
            this.PropertyName = propertyName;
            this.PropertyClassName = propertyClassName;
            this.PropertyCanonicalName = propertyCanonicalName;
            this.PropertyTypeName = PropertyTypeName;
            this.PropertySummary = propertySummary;
        }
    }

    public class PropertyWalker : CSharpSyntaxWalker
    {
        private string startClassName;
        private bool searchNestedClasses;
        private List<string> classesToCallList = new List<string>();
        private string classNodeName;
        private string parentClassNodeName = null;

        public Dictionary<string, Property> PropertyDictionary { get; } = new Dictionary<string, Property>();

        public PropertyWalker(string startClassName, bool searchNestedClasses)
        {
            this.startClassName = startClassName;
            this.searchNestedClasses = searchNestedClasses;
        }

        /// <summary>
        ///Class Visitor
        /// </summary>
        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            classNodeName = node.Identifier.ToString();

            //We have to get the ParentClassNode to build a method call for a property
            if (node.Parent.Kind().ToString() == "ClassDeclaration")
            {
                var parentClassNode = (ClassDeclarationSyntax)node.Parent;
                parentClassNodeName = parentClassNode.Identifier.ToString();
                ClassesToCallProperty(parentClassNodeName, classNodeName);
            }
            //Inital State: Parent of class is namespace or there is no prior code at all.
            else
            {
                ClassesToCallProperty(null, classNodeName);
            }

            base.VisitClassDeclaration(node);
        }

        /// <summary>
        ///Property Visitor
        /// </summary>
        public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            string propertyName = node.Identifier.ToString();
            string propertyCanonicalName = SequenceToCallProperty(classesToCallList, propertyName); //key for our dictionary 
            string propertySummary = GetXMLSummary(node.GetLeadingTrivia());
            string propertyTypeName = node.Type.ToString();

            //First case if nested classes should be excluded.
            if (searchNestedClasses == false && classNodeName == startClassName)
            {
                PropertyDictionary.Add(propertyCanonicalName, (new Property(propertyName, classNodeName, propertyCanonicalName, propertyTypeName, propertySummary)));
            }
            else if (searchNestedClasses == true)
            {
                PropertyDictionary.Add(propertyCanonicalName, (new Property(propertyName, classNodeName, propertyCanonicalName, propertyTypeName, propertySummary)));
            }

            base.VisitPropertyDeclaration(node);
        }

        /// <summary>
        ///Method that maintains a list with class names in the order how they have to be called to get a Property.
        /// </summary>
        public void ClassesToCallProperty(string parentClassNodeName, string classNodeName)
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
        ///Method returns a string which resembles the sequence to call a property. 
        /// </summary>
        private string SequenceToCallProperty(List<string> classesToCallList, string propertyName)
        {
            string returnString = null;

            foreach (string classToCall in classesToCallList)
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
        private string GetXMLSummary(SyntaxTriviaList trivias)
        {
            //Get XML-Trivia from Node
            var xmlCommentTrivia = trivias.FirstOrDefault(t => t.Kind() == SyntaxKind.SingleLineDocumentationCommentTrivia);
            var xml = xmlCommentTrivia.GetStructure();
            if(xml == null){return "";}
            string xmlString = xml.ToString();
            //Get summary from XML-String
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xmlString);
            XmlNodeList xnList = xmlDoc.DocumentElement.SelectNodes("/summary");
            string summary = xnList.Item(0).InnerText;
            //Edit Summary-String            
            summary = summary.Replace("///", "");
            summary = summary.Trim();            

            return summary;
        }
    }

    public class PropertyProcessor
    {
        private Dictionary<string, Property> properties = null;

        public PropertyProcessor(Dictionary<string, Property> properties)
        {
            this.properties = properties;
        }

        /// <summary>
        /// Prints all properties inside the console. 
        /// </summary>
        public void PrintProperties()
        {
            foreach (Property property in properties.Values)
            {
                Console.WriteLine(property.PropertyClassName + "\t" + property.PropertyName + "\t" + property.PropertyCanonicalName + "\t" + property.PropertyTypeName);
                Console.WriteLine(property.PropertySummary + "\n");                
            }
        }

        ///<summary>
        ///This method generates a custom switch case statement to call every property we collected and saves it in a file. 
        ///Edit this method to generate a switch case statement of your liking.
        ///-> Such a task should rather be done with reflection where every value of a property can be accessed via its PropertyInfo.
        ///-> Will cause troubles if the type of the object won't match the type of the property.
        ///</summary>
        //The switch case statement will have following form:
        /*
        switch (expression)
            {                    
            case "ClassName.NestedClassName.PropertyName":
                result = object.ClassName.NestedClassName.PropertyName;
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
                outputFile.WriteLine(fourTabs + "switch (canonicalName)");
                outputFile.WriteLine(fiveTabs + "{");

                foreach (Property property in properties.Values)
                {
                    outputFile.WriteLine(fiveTabs + "case " + @"""" + property.PropertyCanonicalName + @""":");
                    outputFile.WriteLine(sixTabs + "result = object." + property.PropertyCanonicalName + ";");
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
        ///This method creates a csv file which has a line for each property we collected.
        ///Every line will have the class of the property, its name, its canonical name/call sequence, the name of its type and its summary. 
        /// </summary>
        public void CsvPropertyListGenerator(string saveFolderPath)
        {
            using (StreamWriter outputFile = new StreamWriter(saveFolderPath + @"\PropertiesRoslyn.csv"))
            {
                outputFile.WriteLine("sep=,"); // make Excel use comma as field separator
                foreach (Property property in properties.Values)
                {
                    outputFile.WriteLine("{0},{1},{2},{3},{4}", property.PropertyClassName, property.PropertyName, property.PropertyCanonicalName, property.PropertyTypeName, property.PropertySummary);
                }
            }
        }
    }

    /// <summary>
    ///This program extracts some information about properties from a SyntaxTree which was defined in 
    ///the main method. It uses Roslyn to find information about Properties and searches through all 
    ///classes of the Tree to find them. The advantage of using Roslyn over Reflection for this task 
    ///is that it can extract the XML-Summary of a property. The disadvantage of using Roslyn over
    ///Reflection is that you can't get direct access to the values of the Properties. An indirect 
    ///tinkered way to do this is to use the collected information to generate code to call properties 
    ///like it's done with the SwitchCase generator inside the PropertyProcessor class. Other methods
    ///of this class allow you to print all Properties and to write all properties into a csv file. All 
    ///in all this program can be useful to get a better overview over .cs-files with a large number of
    ///properties.
    /// </summary>
    class PropertyCollectorRoslyn
    {
        /// <summary>
        /// This method calls the property walker and returns a dictionary with the canonical name as the key and the
        /// property object as the value. If searchNestedClasses is true it will also return Property(s) from nested 
        /// classes.
        /// </summary>
        public static Dictionary<string, Property> callRoslynWalker(string path, string startClassName, bool searchNestedClasses)
        {
            SyntaxTree tree;

            using (var stream = File.OpenRead(path))
            {
                tree = CSharpSyntaxTree.ParseText(SourceText.From(stream), path: path);
            }

            var root = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Where(n => n.Identifier.ValueText == startClassName).First();

            //Collecting all properties:
            var walker = new PropertyWalker(startClassName, searchNestedClasses);
            walker.Visit(root);

            return walker.PropertyDictionary;
        }

        static void Main(string[] args)
        {
            //Path to test file SyntaxTreeTest.cs:
            string testPath = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.FullName + @"\SyntaxTreeTest.cs";
            
            //Path to the .cs file whose properties we want to extract:              
            /////////////EDIT THIS/////////////
            var path = testPath;
            ///////////////////////////////////

            //Start collecting Properties from a certain class inside the SyntaxTree instead: 
            /////////////EDIT THIS/////////////
            string startClassName = "Car";
            ///////////////////////////////////

            //Collect all properties via Roslyn:
            Dictionary<string, Property> propertyDictionary = callRoslynWalker(path, startClassName, true);

            //Process all properties we collected:
            var properties = new PropertyProcessor(propertyDictionary);

            //Print all properties with description if available: 
            properties.PrintProperties();

            //Create a custom switch case statement which allows us to call every property:
            String desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            //properties.SwitchCaseGenerator(desktopPath);

            //Create a csv file which contains a list of all properties with a description (if available):
            properties.CsvPropertyListGenerator(desktopPath);
        }
    }
}