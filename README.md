Monocle - a simple ORM
=====

Monocle is a very simple Object-relational mapper. It is intended to be used against an SQL Server database - other databases are not supported.

Uses the HyperTypeDescriptor component written by Marc Gravell and described here: http://www.codeproject.com/Articles/18450/HyperDescriptor-Accelerated-dynamic-property-acces

## Usage

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

