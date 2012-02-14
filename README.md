Monocle - a simple ORM
=====

Monocle is a very simple Object-relational mapper. It is intended to be used against an SQL Server database - other databases are not supported.

Uses the HyperTypeDescriptor component written by Marc Gravell and described here: http://www.codeproject.com/Articles/18450/HyperDescriptor-Accelerated-dynamic-property-acces

## Usage

Call the Initialize method to let Monocle know about your database:

    MonocleDb.Initialize(string connectionString);

Mapping a table to a class can be achieved in one of two different ways:

### Persistable

Inherit from Persistable when you want to be able to Insert, Update or Delete an instance of your class (a row in your table) out-of-the-box. You also need to have a column called Id of type uniqueidentifier in the corresponding table.

Inherit from Persistable and put a [Table] attribute above your class declaration. You will also need to implement the Id property.

    [Table]
    public class MyMappedClass : Persistable
    {
        [Column]
        public sealed override Guid Id { get; private set; }

    }

You need a [Column] attribute above each of the properties of your class:

        [Column]
        public string Name { get; set; }

        [Column]
        public DateTime CreatedDate { get; set; }

        [Column]
        public Guid CompanyId { get; set; }

To save an instance of your object to the database, call the Save()-method on the instance. To delete the object, call Delete(). You can override these methods to implement your own logic to supplement or replace the Persistable.Save() / Delete() methods.

### ViewObject

Inherit from ViewObject when you want to map a table to a view. In this case you do not need to override the Id property, but you must include the [Table] and [Column] attributes as normal. You can override the Save() and Delete() methods as necessary to implement persistance logic.

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

Call methods in the static MonocleDb class when you want to perform an action on the current database.

    MonocleDb.Execute("MyJob");

will execute the procedure MyJob.

Monocle can transform a database result to an object using the following method:

    var myPerson = MonocleDb.Execute<Person>("select top 1 * from Person");

If you want to send in parameters, you can use an anonymous object initializer: 

    var myPerson = MonocleDb.Execute<Person>("select top 1 * from Person where id = @Id", new { Id = personId });

But if you want to select someone by their Id, you might just as well use:

    var myPerson = MonocleDb.FindById<Person>(personId);


