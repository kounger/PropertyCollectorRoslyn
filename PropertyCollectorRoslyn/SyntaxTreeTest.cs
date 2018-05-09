namespace PropertyCollectorRoslyn
{
    /// <summary>
    /// This is a test class that provides a SyntaxTree to PropertyCollectorRoslyn.cs
    /// </summary>
    class SyntaxTreeTest
    {
        /// <summary>
        /// This is the summary for property Id.
        /// </summary>
        public int Id
        { get; set; }

        /// <summary>
        /// Internal Class Car.
        /// </summary>
        public class Car
        {
            /// <summary>
            /// This is the summary for property Name.
            /// </summary>
            public string Name
            { get; set; }
            
            public int SerialNumber
            { get; set; }

            /// <summary>
            /// This is the summary for property Automatic.
            /// </summary>
            protected bool Automatic
            { get; set; }

            /// <summary>
            /// Internal Class Interior.
            /// </summary>
            public class Interior
            {
                /// <summary>
                /// This is the summary for property NumberSeats.
                /// </summary>
                public int NumberSeats
                { get; set; }

                /// <summary>
                /// This is the summary for property CupHolder.
                /// </summary>
                public int CupHolder
                { get; set; }
            }
        }
    }
}
