using AutoMapper;
using Vorluno.Planilla.Application.DTOs;
using Vorluno.Planilla.Domain.Entities;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Vorluno.Planilla.Application.Mappings
{
    /// <summary>
    /// Define las reglas de mapeo entre las entidades del dominio y los DTOs.
    /// AutoMapper escanear� este ensamblado en busca de clases que hereden de Profile.
    /// </summary>
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // Mapeo de Entidad a DTO (para operaciones de lectura)
            CreateMap<Empleado, EmpleadoVerDto>()
                .ForMember(dest => dest.DepartamentoNombre, opt => opt.MapFrom(src => src.Departamento != null ? src.Departamento.Nombre : null))
                .ForMember(dest => dest.PosicionNombre, opt => opt.MapFrom(src => src.Posicion != null ? src.Posicion.Nombre : null))
                .ForMember(dest => dest.TieneAccesoSistema, opt => opt.MapFrom(src => !string.IsNullOrEmpty(src.UserId)))
                .ForMember(dest => dest.RolSistema, opt => opt.Ignore()); // Se calcula en el controller

            // Mapeo de DTO a Entidad (para operaciones de escritura/actualizaci�n)
            CreateMap<EmpleadoCrearDto, Empleado>();
            CreateMap<EmpleadoActualizarDto, Empleado>();
        }
    }
}