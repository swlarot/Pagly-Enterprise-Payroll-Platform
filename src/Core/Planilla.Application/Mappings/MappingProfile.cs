using AutoMapper;
using Vorluno.Planilla.Application.DTOs;
using Vorluno.Planilla.Domain.Entities;

namespace Vorluno.Planilla.Application.Mappings
{
    /// <summary>
    /// Define las reglas de mapeo entre las entidades del dominio y los DTOs.
    /// AutoMapper escaneará este ensamblado en busca de clases que hereden de Profile.
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
                .ForMember(dest => dest.RolSistema, opt => opt.Ignore())
                .ForMember(dest => dest.PayPeriodTypeName, opt => opt.MapFrom(src => src.PayPeriodType.ToString()))
                .ForMember(dest => dest.HoursPerWeek, opt => opt.MapFrom(src => src.HoursPerWeek))
                .ForMember(dest => dest.HoursPerPeriod, opt => opt.MapFrom(src => src.HoursPerPeriod))
                .ForMember(dest => dest.HourlyRate, opt => opt.MapFrom(src => src.HourlyRate));

            // Mapeo de DTO a Entidad (para operaciones de escritura/actualización)
            CreateMap<EmpleadoCrearDto, Empleado>()
                .ForMember(dest => dest.HoursPerPeriod, opt => opt.Ignore())
                .AfterMap((src, dest) =>
                {
                    if (src.HoursPerPeriod == null || src.HoursPerPeriod == 0)
                    {
                        dest.HoursPerPeriod = Empleado.CalculateSuggestedHoursPerPeriod(
                            dest.HoursPerWeek, dest.PayPeriodType);
                    }
                    else
                    {
                        dest.HoursPerPeriod = src.HoursPerPeriod.Value;
                    }
                    dest.RecalculateHourlyRate();
                    dest.SyncPayFrequencyFromType();
                });

            CreateMap<EmpleadoActualizarDto, Empleado>()
                .ForMember(dest => dest.HoursPerPeriod, opt => opt.Ignore())
                .AfterMap((src, dest) =>
                {
                    if (src.HoursPerPeriod == null || src.HoursPerPeriod == 0)
                    {
                        dest.HoursPerPeriod = Empleado.CalculateSuggestedHoursPerPeriod(
                            dest.HoursPerWeek, dest.PayPeriodType);
                    }
                    else
                    {
                        dest.HoursPerPeriod = src.HoursPerPeriod.Value;
                    }
                    dest.RecalculateHourlyRate();
                    dest.SyncPayFrequencyFromType();
                });
        }
    }
}
