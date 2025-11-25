using BFFAPIGW.Services;
using ECommerceGRPC.UserService;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc;

namespace BFFAPIGW.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserService.UserServiceClient _userClient;
        private readonly IJwtService _jwtService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            UserService.UserServiceClient userClient,
            IJwtService jwtService,
            ILogger<AuthController> logger)
        {
            _userClient = userClient;
            _jwtService = jwtService;
            _logger = logger;
        }

        /// <summary>
        /// Registrar un nuevo usuario
        /// </summary>
        [HttpPost("register")]
        [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                _logger.LogInformation("Registrando nuevo usuario: {Email}", request.Email);

                var createUserRequest = new CreateUserRequest
                {
                    Email = request.Email,
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    Password = request.Password,
                    Role = request.Role ?? "Customer"
                };

                var userResponse = await _userClient.CreateUserAsync(createUserRequest);

                var token = _jwtService.GenerateToken(
                    userResponse.Id,
                    userResponse.Email,
                    userResponse.Role);

                var response = new RegisterResponse
                {
                    Success = true,
                    Message = "Usuario registrado exitosamente",
                    Token = token,
                    User = new UserDto
                    {
                        Id = userResponse.Id,
                        Email = userResponse.Email,
                        FirstName = userResponse.FirstName,
                        LastName = userResponse.LastName,
                        Role = userResponse.Role
                    }
                };

                return CreatedAtAction(nameof(Register), response);
            }
            catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.AlreadyExists)
            {
                _logger.LogWarning("Email ya registrado: {Email}", request.Email);
                return BadRequest(new { success = false, message = "El email ya está registrado" });
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Error gRPC al registrar usuario");
                return StatusCode(503, new { success = false, message = "Servicio de usuarios no disponible" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al registrar usuario");
                return StatusCode(500, new { success = false, message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Autenticar usuario y obtener token JWT
        /// </summary>
        [HttpPost("login")]
        [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                _logger.LogInformation("Intento de login para: {Email}", request.Email);

                //var authRequest = new AuthenticateRequest
                //{
                //    Email = request.Email,
                //    Password = request.Password
                //};

                //var authResponse = await _userClient.AuthenticateUserAsync(authRequest);

                var authRequest = new ECommerceGRPC.UserService.AuthenticateUserRequest
                {
                    Email = request.Email,
                    Password = request.Password
                };

                var authResponse = await _userClient.AuthenticateUserAsync(authRequest);

                if (!authResponse.Success)
                {
                    _logger.LogWarning("Login fallido para: {Email}", request.Email);
                    return Unauthorized(new { success = false, message = "Credenciales inválidas" });
                }

                var token = _jwtService.GenerateToken(
                    authResponse.User.Id,
                    authResponse.User.Email,
                    authResponse.User.Role);

                var response = new LoginResponse
                {
                    Success = true,
                    Message = "Login exitoso",
                    Token = token,
                    User = new UserDto
                    {
                        Id = authResponse.User.Id,
                        Email = authResponse.User.Email,
                        FirstName = authResponse.User.FirstName,
                        LastName = authResponse.User.LastName,
                        Role = authResponse.User.Role
                    }
                };

                _logger.LogInformation("Login exitoso para usuario: {UserId}", authResponse.User.Id);

                return Ok(response);
            }
            catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
            {
                _logger.LogWarning("Usuario no encontrado: {Email}", request.Email);
                return Unauthorized(new { success = false, message = "Credenciales inválidas" });
            }
            catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.Unauthenticated)
            {
                _logger.LogWarning("Credenciales inválidas para: {Email}", request.Email);
                return Unauthorized(new { success = false, message = "Credenciales inválidas" });
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Error gRPC al autenticar usuario");
                return StatusCode(503, new { success = false, message = "Servicio de autenticación no disponible" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al autenticar usuario");
                return StatusCode(500, new { success = false, message = "Error interno del servidor" });
            }
        }
    }

    // DTOs para el AuthController
    public class RegisterRequest
    {
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? Role { get; set; }
    }

    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class RegisterResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public UserDto User { get; set; } = null!;
    }

    public class LoginResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public UserDto User { get; set; } = null!;
    }

    public class UserDto
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }
}
/////////////////////////////////////////////
//using BFFAPIGW.Services;
//using ECommerceGRPC.UserService;
//using Grpc.Core;
//using Microsoft.AspNetCore.Mvc;

//namespace BFFAPIGW.Controllers
//{

//    [ApiController]
//    [Route("api/[controller]")]
//    public class AuthController : ControllerBase
//    {
//        private readonly UserService.UserServiceClient _userClient;
//        private readonly IJwtService _jwtService;
//        private readonly ILogger<AuthController> _logger;

//        public AuthController(
//            UserService.UserServiceClient userClient,
//            IJwtService jwtService,
//            ILogger<AuthController> logger)
//        {
//            _userClient = userClient;
//            _jwtService = jwtService;
//            _logger = logger;
//        }

//        /// <summary>
//        /// Registrar un nuevo usuario
//        /// </summary>
//        [HttpPost("register")]
//        [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status201Created)]
//        [ProducesResponseType(StatusCodes.Status400BadRequest)]
//        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
//        {
//            try
//            {
//                _logger.LogInformation("Registrando nuevo usuario: {Email}", request.Email);

//                var createUserRequest = new CreateUserRequest
//                {
//                    Email = request.Email,
//                    FirstName = request.FirstName,
//                    LastName = request.LastName,
//                    Password = request.Password,
//                    Role = request.Role ?? "Customer"
//                };

//                var userResponse = await _userClient.CreateUserAsync(createUserRequest);

//                // Generar token JWT para el nuevo usuario
//                var token = _jwtService.GenerateToken(
//                    userResponse.Id,
//                    userResponse.Email,
//                    userResponse.Role);

//                var response = new RegisterResponse
//                {
//                    Success = true,
//                    Message = "Usuario registrado exitosamente",
//                    Token = token,
//                    User = new UserDto
//                    {
//                        Id = userResponse.Id,
//                        Email = userResponse.Email,
//                        FirstName = userResponse.FirstName,
//                        LastName = userResponse.LastName,
//                        Role = userResponse.Role
//                    }
//                };

//                return CreatedAtAction(nameof(Register), response);
//            }
//            catch (RpcException ex) when (ex.StatusCode == StatusCode.AlreadyExists)
//            {
//                _logger.LogWarning("Email ya registrado: {Email}", request.Email);
//                return BadRequest(new { success = false, message = "El email ya está registrado" });
//            }
//            catch (RpcException ex)
//            {
//                _logger.LogError(ex, "Error gRPC al registrar usuario");
//                return BadRequest(new { success = false, message = ex.Status.Detail });
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error inesperado al registrar usuario");
//                return StatusCode(500, new { success = false, message = "Error interno del servidor" });
//            }
//        }

//        /// <summary>
//        /// Autenticar usuario y obtener token JWT
//        /// </summary>
//        [HttpPost("login")]
//        [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
//        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
//        public async Task<IActionResult> Login([FromBody] LoginRequest request)
//        {
//            try
//            {
//                _logger.LogInformation("Intento de login para: {Email}", request.Email);

//                var authRequest = new AuthenticateRequest
//                {
//                    Email = request.Email,
//                    Password = request.Password
//                };

//                var authResponse = await _userClient.AuthenticateUserAsync(authRequest);

//                if (!authResponse.Success)
//                {
//                    _logger.LogWarning("Login fallido para: {Email}", request.Email);
//                    return Unauthorized(new { success = false, message = "Credenciales inválidas" });
//                }

//                // El servicio gRPC ya devuelve un token, pero regeneramos uno nuevo con nuestras configuraciones
//                var token = _jwtService.GenerateToken(
//                    authResponse.User.Id,
//                    authResponse.User.Email,
//                    authResponse.User.Role);

//                var response = new LoginResponse
//                {
//                    Success = true,
//                    Message = "Login exitoso",
//                    Token = token,
//                    User = new UserDto
//                    {
//                        Id = authResponse.User.Id,
//                        Email = authResponse.User.Email,
//                        FirstName = authResponse.User.FirstName,
//                        LastName = authResponse.User.LastName,
//                        Role = authResponse.User.Role
//                    }
//                };

//                _logger.LogInformation("Login exitoso para usuario: {UserId}", authResponse.User.Id);

//                return Ok(response);
//            }
//            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
//            {
//                _logger.LogWarning("Usuario no encontrado: {Email}", request.Email);
//                return Unauthorized(new { success = false, message = "Credenciales inválidas" });
//            }
//            catch (RpcException ex) when (ex.StatusCode == StatusCode.Unauthenticated)
//            {
//                _logger.LogWarning("Credenciales inválidas para: {Email}", request.Email);
//                return Unauthorized(new { success = false, message = "Credenciales inválidas" });
//            }
//            catch (RpcException ex)
//            {
//                _logger.LogError(ex, "Error gRPC al autenticar usuario");
//                return StatusCode(503, new { success = false, message = "Servicio de autenticación no disponible" });
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error inesperado al autenticar usuario");
//                return StatusCode(500, new { success = false, message = "Error interno del servidor" });
//            }
//        }
//    }

//    // DTOs para el AuthController
//    public class RegisterRequest
//    {
//        public string Email { get; set; } = string.Empty;
//        public string FirstName { get; set; } = string.Empty;
//        public string LastName { get; set; } = string.Empty;
//        public string Password { get; set; } = string.Empty;
//        public string? Role { get; set; }
//    }

//    public class LoginRequest
//    {
//        public string Email { get; set; } = string.Empty;
//        public string Password { get; set; } = string.Empty;
//    }

//    public class RegisterResponse
//    {
//        public bool Success { get; set; }
//        public string Message { get; set; } = string.Empty;
//        public string Token { get; set; } = string.Empty;
//        public UserDto User { get; set; } = null!;
//    }

//    public class LoginResponse
//    {
//        public bool Success { get; set; }
//        public string Message { get; set; } = string.Empty;
//        public string Token { get; set; } = string.Empty;
//        public UserDto User { get; set; } = null!;
//    }

//    public class UserDto
//    {
//        public int Id { get; set; }
//        public string Email { get; set; } = string.Empty;
//        public string FirstName { get; set; } = string.Empty;
//        public string LastName { get; set; } = string.Empty;
//        public string Role { get; set; } = string.Empty;
//    }
//}
