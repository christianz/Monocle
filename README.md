Monocle - a simple ORM
=====

Monocle is a very simple Object-relational mapper. It is intended to be used against an SQL Server database - other databases are not supported.

Uses the HyperTypeDescriptor component written by Marc Gravell and described here: http://www.codeproject.com/Articles/18450/HyperDescriptor-Accelerated-dynamic-property-acces

## Usage

### Getting started

Call the Initialize method to let Monocle know about your database:

    MonocleDb.Initialize(string connectionString);

### Database requirements

If you want Insert/Update/Delete functionality for free, your database tables need to have an Id column of type uniqueidentifier.

### Persistable

Inherit from Persistable when you want to be able to Insert, Update or Delete an instance of your class (a row in your table) out-of-the-box. You also need to have a column called Id of type uniqueidentifier in the corresponding table.

Optionally, include a [Table] attribute at the top of your class declaration. 

#### The Table attribute

The Table attribute has the following properties:

 * TableName - the name of the table in the database. If left empty, use the same name as your class.
 * AutoMap (default false) - If true, implicitly maps properties (useful for DTOs).

If Monocle doesn't find a [Table] attribute above your class, it will use a table with the same name as your class in the database, and require any properties you want mapped to your table to be explicitly
defined with a [Column] attribute.

    public class MyMappedClass : Persistable
    {
        [Column]
        public string Name { get; set; }

		[Column]
		public DateTime CreatedDate { get; set; }
    }

To save an instance of your object to the database, call the Save()-method on the instance. 
	
	var myMappedClass = new MyMappedClass
						{
							Name = "Hi, Github!",
							CreatedDate = DateTime.Now
						};

	myMappedClass.Save();

To delete the object, call Delete(). You can override these methods to implement your own logic to supplement or replace the Persistable.Save() / Delete() methods.

### ViewObject

Inherit from ViewObject when you want to map a table to a view. You must include [Column] attributes. You can override the Save() and Delete() methods as necessary to implement persistance logic.

    [Table]
    public class MyViewObject : ViewObject
    {
        [Column]
        public string Name { get; set; }

        public override void Save()
        {
            // Save logic here.
        }
    }

### MonocleDb

Call methods in the static MonocleDb class when you want to perform an action on the current database. All methods take in either a stored procedure or a text command. You do not need to specify which is which.

    MonocleDb.Execute("MyJob");

Will execute the procedure MyJob.

Monocle can transform a database result to an object using the following method:

    var myPerson = MonocleDb.Execute<Person>("select top 1 * from Person");

If you want to send in parameters, you can use an anonymous object initializer: 

    var myPerson = MonocleDb.Execute<Person>("select top 1 * from Person where id = @Id", new { Id = personId });

If you want to select something by its Id, you can use:

    var myPerson = MonocleDb.FindById<Person>(personId);

To fetch all records of something, use:

	var myPeople = MonocleDb.List<Person>();

Or to use a selector:

	var myPeople = MonocleDb.ExecuteList<Person>("select top 100 * from Person where name = @name", new { name = "Christian" });

