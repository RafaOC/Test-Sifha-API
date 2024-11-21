using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Threading.Tasks;
using BCrypt.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using TestAPI.Models;

namespace TestAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PersonaController : ControllerBase
    {
        private readonly string cadenaSQL;

        public PersonaController(IConfiguration config)
        {
            cadenaSQL = config.GetConnectionString("CadenaSQL");
        }

        [HttpGet("{id}")]
        public IActionResult ObtenerPerfil(int id)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(cadenaSQL))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand("PROC_ListarDatosPersonaPorIdUsuario", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@IdUsuario", id);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var perfil = new
                                {
                                    IdPersona = reader.GetInt32(reader.GetOrdinal("id_persona")),
                                    Nombre = reader.GetString(reader.GetOrdinal("nombre")),
                                    Apellido = reader.GetString(reader.GetOrdinal("apellido")),
                                    Direccion = reader.GetString(reader.GetOrdinal("direccion")),
                                    Cedula = reader.IsDBNull(reader.GetOrdinal("cedula")) ? null : reader.GetString(reader.GetOrdinal("cedula")),
                                    Foto = reader.IsDBNull(reader.GetOrdinal("foto")) ? null : reader.GetString(reader.GetOrdinal("foto")),
                                    Correo = reader.IsDBNull(reader.GetOrdinal("correo")) ? null : reader.GetString(reader.GetOrdinal("correo")),
                                    Nacionalidad = reader.IsDBNull(reader.GetOrdinal("nacionalidad")) ? null : reader.GetString(reader.GetOrdinal("nacionalidad")),
                                    Genero = reader.IsDBNull(reader.GetOrdinal("genero")) ? null : reader.GetString(reader.GetOrdinal("genero")),
                                    IdUsuario = reader.GetInt32(reader.GetOrdinal("id_usuario")),
                                    NombreUsuario = reader.GetString(reader.GetOrdinal("nombre_usuario"))
                                };
                                return Ok(perfil);
                            }
                            else
                            {
                                return NotFound("Usuario no encontrado");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, $"Error al obtener el perfil del usuario: {ex.Message}");
            }
        }

        [HttpPost("actualizar")]
        public async Task<IActionResult> ActualizarPerfil([FromForm] EditarPerfil model)
        {
            try
            {
                string nuevaFotoPath = null;

                using (SqlConnection conn = new SqlConnection(cadenaSQL))
                {
                    conn.Open();

                    // Procesar la nueva foto si se proporciona
                    if (model.Foto != null)
                    {
                        var fotoFileName = Path.GetFileName(model.Foto.FileName);
                        var fotoDirectory = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images", model.IdUsuario.ToString());
                        if (Directory.Exists(fotoDirectory))
                        {
                            Directory.Delete(fotoDirectory, true); // Eliminar foto anterior
                        }
                        Directory.CreateDirectory(fotoDirectory);

                        nuevaFotoPath = Path.Combine(fotoDirectory, fotoFileName);

                        using (var stream = new FileStream(nuevaFotoPath, FileMode.Create))
                        {
                            await model.Foto.CopyToAsync(stream);
                        }

                        nuevaFotoPath = $"images/{model.IdUsuario}/{fotoFileName}";
                    }

                    // Encriptar nueva contraseña si se proporciona
                    string hashedPassword = null;
                    if (!string.IsNullOrEmpty(model.NuevaContraseña))
                    {
                        hashedPassword = BCrypt.Net.BCrypt.HashPassword(model.NuevaContraseña);
                    }

                    // Actualizar perfil utilizando el procedimiento almacenado
                    using (SqlCommand cmd = new SqlCommand("PROC_EditarDatosPersonaPorIdUsuario", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@IdUsuario", model.IdUsuario);
                        cmd.Parameters.AddWithValue("@Nombre", (object)model.Nombre ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Apellido", (object)model.Apellido ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Direccion", (object)model.Direccion ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Nacionalidad", (object)model.IdNacionalidad ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Genero", (object)model.IdGenero ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Foto", (object)nuevaFotoPath ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@NombreUsuario", (object)model.NombreUsuario ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Correo", (object)model.Correo ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Contrasena", (object)hashedPassword ?? DBNull.Value);

                        cmd.ExecuteNonQuery();
                    }
                }

                return Ok("Perfil actualizado correctamente");
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, $"Error al actualizar el perfil del usuario: {ex.Message}");
            }
        }

        [HttpDelete("{id}")]
        public IActionResult EliminarCuentaUsuario(int id)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(cadenaSQL))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand("PROC_EliminarCuentaUsuario", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@id_usuario", id);

                        int rowsAffected = cmd.ExecuteNonQuery();
                        if (rowsAffected > 0)
                        {
                            return Ok("Cuenta eliminada correctamente");
                        }
                        else
                        {
                            return NotFound("Usuario no encontrado");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, $"Error al eliminar la cuenta del usuario: {ex.Message}");
            }
        }

        

    }
}
