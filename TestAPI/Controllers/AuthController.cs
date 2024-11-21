using System;
using System.Data;
using System.Data.SqlClient;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using BCrypt.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Win32;
using TestAPI.Models;

namespace TestAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly string cadenaSQL;
        private readonly string secretkey;
        private readonly string _imageFolderPath;
        

        public AuthController(IConfiguration configuration)
        {
            cadenaSQL = configuration.GetConnectionString("CadenaSQL");
            secretkey = configuration.GetSection("settings").GetSection("secretkey").ToString();
            _imageFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/");
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] UserLogin login)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(cadenaSQL))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand("PROC_UsuarioLogin", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@NombreUsuarioOCorreo", login.NombreUsuario); // Puede ser correo o nombre de usuario

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read() && reader["id_usuario"] != DBNull.Value)
                            {
                                string storedHash = reader["hash_contraseña"].ToString();

                                if (BCrypt.Net.BCrypt.Verify(login.HashContraseña, storedHash))
                                {
                                    var keyBytes = Encoding.ASCII.GetBytes(secretkey);
                                    var claims = new ClaimsIdentity();
                                    claims.AddClaim(new Claim(ClaimTypes.NameIdentifier, login.NombreUsuario));

                                    var tokenDescriptior = new SecurityTokenDescriptor
                                    {
                                        Subject = claims,
                                        Expires = DateTime.UtcNow.AddMinutes(2400),
                                        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256Signature),
                                    };

                                    var tokenHandler = new JwtSecurityTokenHandler();
                                    var tokenConfig = tokenHandler.CreateToken(tokenDescriptior);

                                    string tokenCreado = tokenHandler.WriteToken(tokenConfig);

                                    int userId = (int)reader["id_usuario"];
                                    return Ok(new { UserId = userId, Token = tokenCreado, Mensaje = "Inicio de sesión exitoso" });
                                }
                                else
                                {
                                    return Unauthorized(new { Mensaje = "Credenciales inválidas" });
                                }
                            }
                            else
                            {
                                return Unauthorized(new { Mensaje = "Credenciales inválidas" });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return StatusCode(500, new { Mensaje = "Ocurrió un error en el servidor" });
            }
        }


        [HttpPost("signup")]
        public async Task<IActionResult> Signup([FromForm] UserSignUp signup)
        {
            string filePath = null;
            try
            {
                // Procesar la foto solo si el usuario la proporciona
                if (signup.Foto != null && signup.Foto.Length > 0)
                {
                    if (!Directory.Exists(_imageFolderPath))
                    {
                        Directory.CreateDirectory(_imageFolderPath);
                    }

                    var fileName = Path.GetRandomFileName() + Path.GetExtension(signup.Foto.FileName);
                    filePath = Path.Combine(_imageFolderPath, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await signup.Foto.CopyToAsync(stream);
                    }

                    filePath = $"images/{fileName}";
                }

                using (SqlConnection conn = new SqlConnection(cadenaSQL))
                {
                    conn.Open();

                    // Verificar duplicados
                    using (SqlCommand verificar = new SqlCommand("PROC_VerificarUsuarioCorreoCedula", conn))
                    {
                        verificar.CommandType = CommandType.StoredProcedure;
                        verificar.Parameters.AddWithValue("@NombreUsuario", signup.NombreUsuario);
                        verificar.Parameters.AddWithValue("@Correo", signup.Correo);
                        verificar.Parameters.AddWithValue("@Cedula", signup.Cedula);

                        string resultado = verificar.ExecuteScalar()?.ToString();

                        if (!string.IsNullOrEmpty(resultado))
                        {
                            string mensaje = resultado switch
                            {
                                "NombreUsuario" => "El nombre de usuario ya existe",
                                "Correo" => "El correo ya está registrado",
                                "Cedula" => "La cédula ya está registrada",
                                _ => "Error desconocido"
                            };
                            return Conflict(new { Mensaje = mensaje });
                        }
                    }

                    // Encriptar la contraseña
                    string hashedPassword = BCrypt.Net.BCrypt.HashPassword(signup.Contraseña);

                    // Registrar al usuario
                    using (SqlCommand cmd = new SqlCommand("PROC_RegistrarUsuario", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Nombre", signup.Nombre);
                        cmd.Parameters.AddWithValue("@Apellido", signup.Apellido);
                        cmd.Parameters.AddWithValue("@Direccion", signup.Direccion);
                        cmd.Parameters.AddWithValue("@Foto", filePath ?? string.Empty);
                        cmd.Parameters.AddWithValue("@Cedula", signup.Cedula);
                        cmd.Parameters.AddWithValue("@ID_Nacionalidad", signup.IdNacionalidad);
                        cmd.Parameters.AddWithValue("@ID_Genero", signup.IdGenero);
                        cmd.Parameters.AddWithValue("@NombreUsuario", signup.NombreUsuario);
                        cmd.Parameters.AddWithValue("@HashContraseña", hashedPassword);
                        cmd.Parameters.AddWithValue("@Correo", signup.Correo);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, $"Error al registrar el usuario en la base de datos: {ex.Message}");
            }

            return Ok(new { Mensaje = "Usuario registrado exitosamente" });
        }


        [HttpGet("nacionalidades")]
        public async Task<IActionResult> GetNacionalidades()
        {
            var nacionalidades = new List<Nacionalidad>();

            using (SqlConnection conn = new SqlConnection(cadenaSQL))
            {
                string query = "SELECT id_nacionalidad, nombre FROM Nacionalidad";
                SqlCommand command = new SqlCommand(query, conn);

                try
                {
                    await conn.OpenAsync();
                    SqlDataReader reader = await command.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        nacionalidades.Add(new Nacionalidad
                        {
                            IdNacionalidad = reader.GetInt32(0),
                            Nombre = reader.GetString(1)
                        });
                    }
                    reader.Close();
                }
                catch (Exception ex)
                {
                    return StatusCode(500, $"Internal server error: {ex.Message}");
                }
            }

            return Ok(nacionalidades);
        }

        [HttpGet("generos")]
        public async Task<IActionResult> GetGeneros()
        {
            var generos = new List<Genero>();

            using (SqlConnection conn = new SqlConnection(cadenaSQL))
            {
                string query = "SELECT id_genero, texto FROM Genero";
                SqlCommand command = new SqlCommand(query, conn);

                try
                {
                    await conn.OpenAsync();
                    SqlDataReader reader = await command.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        generos.Add(new Genero
                        {
                            IdGenero = reader.GetInt32(0),
                            Texto = reader.GetString(1)
                        });
                    }
                    reader.Close();
                }
                catch (Exception ex)
                {
                    return StatusCode(500, $"Internal server error: {ex.Message}");
                }
            }

            return Ok(generos);
        }


    }
}
