using AutoMapper;
using XncOptimizerUI.MVVM.Models;
using XncOptimizerUI.MVVM.ViewModels;

namespace XncOptimizerUI.Configuration
{
    public class AutoMapperConfiguration
    {
        public static Mapper InitializeAutoMapper()
        {
            var configuration = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Part, PartVM>();
            });

            return new Mapper(configuration);
        }
    }
}
