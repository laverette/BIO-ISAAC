public class Database
{
public string host {get; set;}
public string database {get; set;}
public string username {get; set;}
public string password {get; set;}
public string connectionString {get; set;}
public string port {get; set;}
public string cs {get; set;}

public Database(string host, string database, string username, string password, string port)
{
    host = "pwcspfbyl73eccbn.cbetxkdyhwsb.us-east-1.rds.amazonaws.com";
    database = "fe6rixun2j0cpejc";
    username = "k0c1dk1k6832bhcc";
    password = "bvqvmxpxny52gqh6";
    port = "3306";
    cs = $"Server={host};Port={port};Database={database};User Id={username};Password={password};CharSet=utf8mb4;SslMode=Required;";
}
}