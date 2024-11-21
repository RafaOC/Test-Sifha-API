namespace TestAPI.Models
{
    public class Persona
    {
        public int IdPersona { get; set; }
        public string? Nombre { get; set; }
        public string? Apellido { get; set; }
        public string? Direccion { get; set; }
        public string? Cedula { get; set; }
        public string? Foto { get; set; }
        public string? Correo { get; set; }
        public int? IdNacionalidad { get; set; }
        public int? IdGenero { get; set; }
    }
}
