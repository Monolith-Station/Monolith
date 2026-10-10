# ====================================================================
# Entity Localization File
# Generated: 2026-07-10 20:21:30
# Total entities: 79
# ====================================================================

# From: _FarHorizons/Catalog/Fills/Crates/reactor_crates.yml

ent-CrateBohrumControlRod = caixote de barras de controle
    .desc = 2 barras de controle de bohrum para um reator nuclear.

ent-CrateCerenkiteFuelRod = caixote de barras de combustível
    .desc = 3 barras de combustível de cerenquita, para abastecer um reator nuclear.

ent-CrateSteelGasChannel = caixote de barras de canal de gás
    .desc = 4 barras de canal de gás de aço para um reator nuclear.

ent-CrateSteelHeatExhanger = caixote de barras de trocador de calor
    .desc = 4 barras de trocador de calor de aço para um reator nuclear.

# From: _FarHorizons/Entities/Objects/Devices/Circuitboards/computer.yml

ent-GasTurbineMonitorComputerCircuitboard = placa de computador de monitor de turbina a gás
    .desc = Uma placa de circuito impresso de computador para um monitor de turbina a gás.

ent-NuclearReactorMonitorComputerCircuitboard = placa de computador de monitor de reator nuclear
    .desc = Uma placa de circuito impresso de computador para um monitor de reator nuclear.

# From: _FarHorizons/Entities/Objects/Devices/Circuitboards/Machine/nuclear_machines.yml

ent-NuclearCentrifugeMachineCircuitboard = placa de máquina de centrífuga nuclear
    .desc = Uma placa de circuito impresso de máquina para uma centrífuga nuclear.

ent-NuclearFabricatorMachineCircuitboard = placa de máquina de fabricador nuclear
    .desc = Uma placa de circuito impresso de máquina para um fabricador nuclear. Usa uma estrutura de máquina 2x1.

# From: _FarHorizons/Entities/Objects/Devices/holoprojectors.yml

ent-RadiationBlockingProjector = projetor de barreira de radiação
    .desc = Pare a radiação letal de uma delaminação ou fusão do núcleo, pelo menos por um tempo.

# From: _FarHorizons/Entities/Objects/Materials/ingots.yml

ent-IngotPlutonium = barra de plutônio
    .suffix = Completa
    .desc = Lingotes de plutônio físsil puro. Um subproduto de reatores de fissão nuclear.

ent-IngotPlutonium1 = barra de plutônio
    .desc = { ent-IngotPlutonium.desc }
    .suffix = Única

# From: _FarHorizons/Entities/Structures/Holographic/projections.yml

ent-HolosignRadiationBlocking = barreira de radiação holográfica
    .desc = Uma barreira de luz sólida que bloqueia luz e radiação, mas nada mais.

# From: _FarHorizons/Entities/Structures/Machines/Computers/computers.yml

ent-GasTurbineMonitor = monitor de turbina a gás
    .desc = Um dispositivo que monitora o estado de uma turbina a gás conectada.

ent-NuclearReactorMonitor = monitor de reator nuclear
    .desc = Um dispositivo que monitora o estado de um reator nuclear conectado.

# From: _FarHorizons/Entities/Structures/Machines/nuclear_centrifuge.yml

ent-NuclearCentrifuge = centrífuga de reprocessamento de combustível nuclear
    .desc = Uma máquina grande que pode ser usada para separar isótopos radioativos de combustível gasto. Não certificada para processar minério de urânio bruto.

# From: _FarHorizons/Entities/Structures/Machines/nuclear_fabricator.yml

ent-NuclearFabricator = fabricador nuclear
    .desc = Um fabricador certificado pela ISFNC para fazer componentes certificados para uso em reatores nucleares. É tudo uma grande bobagem.

# From: _FarHorizons/Entities/Structures/Power/Generation/FissionGenerator/nuclear_reactor.yml
# Abstract entity (referenceable by children)

ent-BaseNuclearReactor = reator nuclear
    .desc = Um reator nuclear, com encaixes para barras de combustível e varios outros tipos. Você tem certeza que um desses já explodiu alguma vez.

ent-NuclearDebrisChunk = destroços nucleares
    .desc = Você não vê a grafite no chão. Você está em choque. Procure atendimento médico.

ent-NuclearReactorEmpty = {ent-BaseNuclearReactor}
    .desc = {ent-BaseNuclearReactor.desc}
    .suffix = Vazio

ent-NuclearReactorEmptySalvage = {ent-BaseNuclearReactor}
    .desc = {ent-BaseNuclearReactor.desc}
    .suffix = Vazio, Salvagem

ent-NuclearReactorMeltdown = {ent-BaseNuclearReactor}
    .desc = {ent-BaseNuclearReactor.desc}
    .suffix = Fusão

ent-NuclearReactorMelted = {ent-BaseNuclearReactor}
    .suffix = Derretido
    .desc = Um reator nuclear, derretido há muito tempo. Ainda brilha com calor residual e radiação.

ent-NuclearReactorMeltedSalvage = {ent-BaseNuclearReactor}
    .suffix = Derretido, Salvagem
    .desc = Um reator nuclear, derretido há muito tempo. Ainda brilha com calor residual e radiação.

ent-NuclearReactorNormal = {ent-BaseNuclearReactor}
    .desc = {ent-BaseNuclearReactor.desc}

ent-NuclearReactorNormalSalvage = {ent-BaseNuclearReactor}
    .desc = {ent-BaseNuclearReactor.desc}
    .suffix = Salvagem

ent-NuclearReactorRandom = {ent-BaseNuclearReactor}
    .desc = {ent-BaseNuclearReactor.desc}
    .suffix = Aleatório

ent-NuclearReactorRandomSalvage = {ent-BaseNuclearReactor}
    .desc = {ent-BaseNuclearReactor.desc}
    .suffix = Aleatório, Salvagem

ent-NuclearReactorSmall = reator nuclear pequeno
    .desc = { ent-BaseNuclearReactor.desc }

ent-NuclearReactorSmallMelted = {ent-NuclearReactorSmall}
    .suffix = Derretido
    .desc = Um reator nuclear, derretido há muito tempo. Ainda brilha com calor residual e radiação.

ent-NuclearReactorSmallMeltedSalvage = {ent-NuclearReactorSmall}
    .suffix = Derretido, Salvagem
    .desc = Um reator nuclear, derretido há muito tempo. Ainda brilha com calor residual e radiação.

ent-NuclearReactorSmallRandom = {ent-NuclearReactorSmall}
    .desc = {ent-NuclearReactorSmall.desc}
    .suffix = Aleatório

ent-NuclearReactorSmallRandomSalvage = {ent-NuclearReactorSmall}
    .desc = {ent-NuclearReactorSmall.desc}
    .suffix = Aleatório, Salvagem

ent-NuclearReactorSmallSalvage = reator nuclear pequeno
    .desc = {ent-NuclearReactorSmall.desc}
    .suffix = Salvagem

# From: _FarHorizons/Entities/Structures/Power/Generation/FissionGenerator/reactor_components.yml

ent-BananiumReactorFuelRod = barra de combustível de bananium
    .desc = Uma barra de banânio para um reator nuclear.

ent-BaseReactorControlRod = barra de controle
    .desc = Um conjunto de barras de controle para um reator nuclear.

ent-BaseReactorFuelRod = barra de combustível
    .desc = Uma barra de combustível para um reator nuclear.

ent-BaseReactorGasChannel = barra de canal de gás
    .desc = Um canal de gás para um reator nuclear.

ent-BaseReactorHeatExchanger = barra de trocador de calor
    .desc = Um trocador de calor para um reator nuclear.

ent-BohrumReactorControlRod = barra de controle de bohrum
    .desc = { ent-BaseReactorControlRod.desc }

ent-BrassReactorControlRod = barra de controle de latão
    .desc = { ent-BaseReactorControlRod.desc }

ent-BrassReactorGasChannel = barra de canal de gás de latão
    .desc = { ent-BaseReactorGasChannel.desc }

ent-BrassReactorHeatExchanger = barra de trocador de calor de latão
    .desc = { ent-BaseReactorHeatExchanger.desc }

ent-CerenkiteReactorFuelRod = barra de combustível de cerenquita
    .desc = { ent-BaseReactorFuelRod.desc }

ent-DiamondReactorControlRod = barra de controle de diamante
    .desc = { ent-BaseReactorControlRod.desc }

ent-DiamondReactorGasChannel = barra de canal de gás de diamante
    .desc = { ent-BaseReactorGasChannel.desc }

ent-DiamondReactorHeatExchanger = barra de trocador de calor de diamante
    .desc = { ent-BaseReactorHeatExchanger.desc }

ent-GlassReactorControlRod = barra de controle de vidro
    .desc = { ent-BaseReactorControlRod.desc }

ent-GlassReactorGasChannel = barra de canal de gás de vidro
    .desc = { ent-BaseReactorGasChannel.desc }

ent-GlassReactorHeatExchanger = barra de trocador de calor de vidro
    .desc = { ent-BaseReactorHeatExchanger.desc }

ent-GoldReactorControlRod = barra de controle de ouro
    .desc = { ent-BaseReactorControlRod.desc }

ent-GoldReactorGasChannel = barra de canal de gás de ouro
    .desc = { ent-BaseReactorGasChannel.desc }

ent-GoldReactorHeatExchanger = barra de trocador de calor de ouro
    .desc = { ent-BaseReactorHeatExchanger.desc }

ent-MeatReactorFuelRod = barra de combustível de carne
    .desc = Uma barra de combustí- espera, ela está viva?

ent-PlasmaGlassReactorControlRod = barra de controle de vidro de plasma
    .desc = { ent-BaseReactorControlRod.desc }

ent-PlasmaGlassReactorGasChannel = barra de canal de gás de vidro de plasma
    .desc = { ent-BaseReactorGasChannel.desc }

ent-PlasmaGlassReactorHeatExchanger = barra de trocador de calor de vidro de plasma
    .desc = { ent-BaseReactorHeatExchanger.desc }

ent-PlasmaReactorFuelRod = barra de combustível de plasma
    .desc = { ent-BaseReactorFuelRod.desc }

ent-PlasteelReactorControlRod = barra de controle de plastiaço
    .desc = { ent-BaseReactorControlRod.desc }

ent-PlasteelReactorGasChannel = barra de canal de gás de plastiaço
    .desc = { ent-BaseReactorGasChannel.desc }

ent-PlasteelReactorHeatExchanger = barra de trocador de calor de plastiaço
    .desc = { ent-BaseReactorHeatExchanger.desc }

ent-PlutoniumReactorFuelRod = barra de combustível de plutônio
    .desc = { ent-BaseReactorFuelRod.desc }

ent-SilverReactorControlRod = barra de controle de prata
    .desc = { ent-BaseReactorControlRod.desc }

ent-SilverReactorGasChannel = barra de canal de gás de prata
    .desc = { ent-BaseReactorGasChannel.desc }

ent-SilverReactorHeatExchanger = barra de trocador de calor de prata
    .desc = { ent-BaseReactorHeatExchanger.desc }

ent-SteelReactorControlRod = barra de controle de aço
    .desc = { ent-BaseReactorControlRod.desc }

ent-SteelReactorGasChannel = barra de canal de gás de aço
    .desc = { ent-BaseReactorGasChannel.desc }

ent-SteelReactorHeatExchanger = barra de trocador de calor de aço
    .desc = { ent-BaseReactorHeatExchanger.desc }

ent-UraniumGlassReactorFuelRod = barra de combustível de vidro de urânio
    .desc = { ent-BaseReactorFuelRod.desc }

ent-UraniumReactorFuelRod = barra de combustível de urânio
    .desc = { ent-BaseReactorFuelRod.desc }

# From: _FarHorizons/Entities/Structures/Power/Generation/FissionGenerator/turbine.yml

ent-Turbine = turbina a gás
    .desc = Uma turbina grande usada para gerar energia usando gás quente.

ent-TurbineSmall = turbina a gás pequena
    .desc = Uma turbina usada para gerar energia usando gás quente.

# From: _FarHorizons/Entities/Structures/Power/Generation/FissionGenerator/turbineProjectile.yml

ent-TurbineBladeShrapnel = pá de turbina


# ===== Generated missing entity keys (untranslated) - 2026-10-09 =====

ent-PlanetEntity = Planeta

ent-StarEntity = Estrela
