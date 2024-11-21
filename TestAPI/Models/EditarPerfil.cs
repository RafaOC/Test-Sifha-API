namespace TestAPI.Models
{
    public class EditarPerfil
    {
        public int IdUsuario { get; set; }

        public string? Nombre { get; set; }
        public string? Apellido { get; set; }
        public string? Direccion { get; set; }
        public int? IdNacionalidad { get; set; } 
        public int? IdGenero { get; set; } 
        public IFormFile? Foto { get; set; }
        public string? Correo { get; set; }

        public string? NombreUsuario { get; set; } 
        public string? NuevaContraseña { get; set; } 
    }
}
