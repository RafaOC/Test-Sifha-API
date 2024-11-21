namespace TestAPI.Models
{
    public class UserSignUp
    {
        public string? Nombre { get; set; }
        public string? Apellido { get; set; }
        public string? Direccion { get; set; }
        public string? Cedula { get; set; }
        public IFormFile? Foto { get; set; } 
        public int? IdNacionalidad { get; set; }
        public int? IdGenero { get; set; }
        public string? NombreUsuario { get; set; }
        public string? Contraseña { get; set; }
        public string? Correo { get; set; }
    }
}
