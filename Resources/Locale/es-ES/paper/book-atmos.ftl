book-text-atmos-distro = La red de distribución, o "distro" para abreviar, es el sustento de la estación. Es responsable de transportar el aire desde la sección de atmosféricos por toda la estación.

        Las tuberías relevantes suelen estar pintadas de Azul Suave Llamativo, pero una manera segura de identificarlas es usar un escáner de bandeja para rastrear qué tuberías están conectadas a las ventilaciones activas de la estación.

        La mezcla estándar de gas de la red de distribución es de 20 grados centígrados, 78% nitrógeno, 22% oxígeno. Puedes comprobarlo usando un analizador de gas en una tubería de distro o en cualquier ventilación conectada a ella. Las circunstancias especiales pueden requerir mezclas especiales.

        A la hora de decidir una presión de distro, hay que tener en cuenta algunas cosas. Las ventilaciones activas regularán la presión de la estación, así que mientras todo funcione correctamente, no existe tal cosa como una presión de distro demasiado alta.

        Una presión de distro más alta permitirá que la red de distro actúe como un amortiguador entre los mineros de gas y las ventilaciones, proporcionando una cantidad significativa de aire extra que puede usarse para represurizar la estación tras un vaciado al espacio.

        Una presión de distro más baja reducirá la cantidad de gas perdido en caso de que el distro quede expuesto al espacio, una forma rápida de lidiar con la contaminación del distro. También puede ayudar a ralentizar o prevenir la sobrepresurización de la estación en caso de problemas con las ventilaciones.

        Las presiones de distro comunes están en el rango de 300-375 kPa, pero se pueden usar otras presiones con conocimiento de los riesgos y beneficios.

        La presión de la red está determinada por la última bomba que bombea hacia ella. Para prevenir cuellos de botella, todas las demás bombas entre los mineros y la última bomba deben configurarse a su velocidad máxima, y cualquier dispositivo innecesario debe eliminarse.

        Puedes validar la presión del distro con un analizador de gas, pero ten en cuenta que una alta demanda debida a cosas como vaciados al espacio puede causar que el distro esté por debajo de la presión objetivo establecida durante períodos prolongados. Así que, si ves una caída en la presión, no entres en pánico - puede ser temporal.

book-text-atmos-waste = La red de residuos es el sistema principal responsable de mantener el aire de la estación libre de contaminantes.

        Puedes identificar las tuberías relevantes por su color Rojo Apagado Agradable o usando un escáner de bandeja para rastrear qué tuberías están conectadas a los depuradores de la estación.

        La red de residuos se usa para transportar gases residuales ya sea para filtrarlos o expulsarlos al espacio. Lo ideal es mantener la presión a 0 kPa, pero a veces puede estar a una presión baja distinta de cero mientras está en uso.

        Los técnicos tienen la opción de filtrar o expulsar al espacio los gases residuales. Aunque la expulsión al espacio es más rápida, el filtrado permite que los gases sean reutilizados para reciclaje o venta.

        La red de residuos también puede usarse para diagnosticar problemas atmosféricos en la estación. Niveles altos de un gas residual pueden sugerir una fuga importante, mientras que la presencia de gases no residuales puede indicar un problema de configuración del depurador o de conexión física. Si los gases están a alta temperatura, podría indicar un incendio.

book-text-atmos-alarms = Las alarmas de aire se encuentran por toda la estación para permitir la gestión y monitorización de la atmósfera local.

            La interfaz de la alarma de aire proporciona a los técnicos una lista de sensores conectados, sus lecturas y la capacidad de ajustar los umbrales. Estos umbrales se usan para determinar la condición de alarma de la alarma de aire. Los técnicos también pueden usar la interfaz para establecer presiones objetivo para las ventilaciones y configurar las velocidades de operación y los gases objetivo para los depuradores.

            Aunque la interfaz permite el ajuste fino de los dispositivos bajo el control de la alarma de aire, también hay varios modos disponibles para la configuración rápida de la alarma. Estos modos se activan automáticamente cuando cambia el estado de la alarma:
            - Filtrado: El modo predeterminado
            - Filtrado (amplio): Un modo de filtrado que modifica la operación de los depuradores para depurar un área más amplia
            - Llenado: Desactiva los depuradores y establece las ventilaciones a su presión máxima
            - Pánico: Desactiva las ventilaciones y configura los depuradores para aspiración

            Una multiherramienta o un configurador de red puede usarse para vincular dispositivos a las alarmas de aire.

book-text-atmos-vents =
    A continuación se muestra una guía de referencia rápida para varios dispositivos atmosféricos:

                Ventilaciones Pasivas:
                Estas ventilaciones no requieren energía, permiten que los gases fluyan libremente tanto hacia dentro como hacia fuera de la red de tuberías a la que están conectadas.

                Ventilaciones Activas:
                Estas son las ventilaciones más comunes en la estación. Tienen una bomba interna y requieren energía. Por defecto, solo bombearán gases fuera de las tuberías, y solo hasta 101 kPa. Sin embargo, pueden reconfigurarse usando una alarma de aire. También se bloquearán si la sala está por debajo de 1 kPa, para evitar bombear gases al espacio.

                Depuradores de Aire:
                Estos dispositivos permiten que los gases sean eliminados del entorno y enviados a la red de tuberías conectada. Pueden configurarse para seleccionar gases específicos cuando están conectados a una alarma de aire.

                Inyectores de Aire:
                Los inyectores son similares a las ventilaciones activas, pero no tienen bomba interna y no requieren energía. No pueden configurarse, pero pueden seguir bombeando gases hasta presiones mucho más altas.
