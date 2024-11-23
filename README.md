# Documentação Configuração de Terreno Procedural TCC

Este projeto utiliza geração procedural de terrenos, permitindo personalização detalhada de parâmetros como altura, texturas e biomas. Esta documentação descreve os campos configuráveis disponíveis.

## `requerimentos`
### **Para Gerar o Terreno**: Ter um objeto com os scripts EndlessTerrain e TerrainGenerator
*Caso necessite spawnar mobs adicione o script WorldMobSpawner também*
### **Para Criar Mobs**: Ter no objeto do MOB MobActionsController, MobStatusController, MobMovementStateMachine, RotateToGroundNormal, HealthManager, SpeedManager 
*Caso deseje que ele persiga jogadores tem de se adicionar uma LayerMask Player uma Tag Player o objeto do Player Deve Ter um PlayerStatusController com um método ReceiveDamage que recebe um inteiro*

---

## 🎛️ **EndlessTerrain**

### `maxViewDst`
- **Tipo**: `float`
- **Descrição**: Define a distância que um chunk deve estar do jogador para ele ser vísivel, se ele estiver além da distância será descarregado.
- **Valor Padrão**: `450`
- **Obrigatório não nulo**: `Sim`
  
### `viewer`
- **Tipo**: `Transform `
- **Descrição**: O Transform do player a ser comparada a posição dele com em relação as do chunks 
- **Valor Padrão**: `null`
- **Obrigatório não nulo**: `Sim`

### `shouldHaveMaxChunkPerSide`
- **Tipo**: `bool `
- **Descrição**: O Terreno a ser gerado é infinito(false) ou finito(true)
- **Valor Padrão**: `true`
- **Obrigatório não nulo**: `Sim` 

### `maxChunksPerSide `
- **Tipo**: `int `
- **Descrição**: Qual tamanho máximo de chunks por lado que o terreno deve ter (caso seja um terreno finito)
- **Valor Padrão**: `5`
- **Obrigatório não nulo**: `Sim` *depende do que foi configurado*

---

## 🎛️ **TerrainGenerator**

### `Octaves`
- **Tipo**: `int`
- **Descrição**: Define o número de camadas de ruído usadas na geração do terreno. Valores mais altos adicionam mais detalhes, enquanto valores baixos resultam em terrenos mais simples.
- **Valor Padrão**: `5`
- **Obrigatório não nulo**: `Sim`

### `Lacunarity`
- **Tipo**: `float`
- **Descrição**: Define o espaçamento relativo entre as frequências das camadas de ruído. Valores maiores aumentam a complexidade.
- **Valor Padrão**: `2.0f`
- **Obrigatório não nulo**: `Sim`

### `DefaultTexture`
- **Tipo**: `Texture2D`
- **Descrição**: Textura padrão aplicada ao terreno, caso nenhuma textura específica seja definida.
- **Valor Padrão**: `null`
- **Obrigatório não nulo**: `Sim`

### `SplatMapShader`
- **Tipo**: `ComputeShader`
- **Descrição**: Compute Shader responsável por processar e aplicar texturas ao terreno baseado em informações como altura e biomas.
  
  *Já existe um no projeto, basta referenciar ele na variável, mas caso deseja pode criar o seu próprio*
- **Valor Padrão**: `null`
- **Obrigatório não nulo**: `Sim`

### `TerrainTextureBasedOnVoronoiPoints`
- **Tipo**: `bool`
- **Descrição**: Ativa ou desativa a utilização de texturas baseadas em pontos de Voronoi no terreno.
- **Valor Padrão**: `true`
- **Obrigatório**: `Sim`

### `NumVoronoiPoints`
- **Tipo**: `int`
- **Descrição**: Número de pontos de Voronoi a serem gerados. Afeta o padrão de divisão do terreno em "células".
- **Valor Padrão**: `3`
- **Obrigatório não nulo**: `Sim`

### `VoronoiSeed`
- **Tipo**: `int`
- **Descrição**: Semente aleatória usada para gerar pontos de Voronoi. Alterar esse valor gera padrões diferentes para o mesmo número de pontos.
- **Valor Padrão**: `0`
- **Obrigatório não nulo**: `Sim`
  
### `VoronoiScale`
- **Tipo**: `float`
- **Descrição**: Controla o tamanho das células de Voronoi no terreno.
- **Valor Padrão**: `1`
- **Obrigatório não nulo**: `Sim`

### `Biomes `
- **Tipo**: ` Biome[]`
- **Descrição**: Define os biomas que terão no terreno.
- **Valor Padrão**: `null`
- **Obrigatório não nulo**: `Sim`

### `LevelOfDetail`
- **Tipo**: `int` (com `Range(0, 6)`)
- **Descrição**: Define o nível de detalhe do terreno. Valores mais altos diminuem a resolução (menor número de triângulos), melhorando o desempenho.
- **Valor Padrão**: `0`
- **Obrigatório não nulo**: `Sim` *depende do que foi configurado*

### `ShouldSpawnObjects`
- **Tipo**: `bool`
- **Descrição**: Determina se objetos (como árvores ou pedras) devem ser gerados no terreno.
- **Valor Padrão**: `true`
- **Obrigatório não nulo**: `Sim`

---

## 🌳 **Classe `Biome`**

Os biomas definem áreas específicas do terreno com suas próprias características visuais e funcionais.

### **Campos da Classe `Biome`**

#### `Name`
- **Tipo**: `string`
- **Descrição**: Nome do bioma (ex.: "Montanha", "Deserto").
- **Valor Padrão**: `null`
- **Obrigatório não nulo**: `Não` 

#### `MinHeight` e `MaxHeight`
- **Tipo**: `float`
- **Descrição**: Altura mínima e máxima em que o bioma será aplicado no terreno aplicado caso a textura seja baseado em altura.
- **Valor Padrão**: `null`
- **Obrigatório não nulo**: `Não` *depende do que foi configurado*

#### `Texture`
- **Tipo**: `Texture2D`
- **Descrição**: Textura específica aplicada a este bioma.
- **Valor Padrão**: `null`
- **Obrigatório não nulo**: `Sim`

#### `Amplitude`
- **Tipo**: `float`
- **Descrição**: Define a variação máxima de altura dentro do bioma.
- **Valor Padrão**: `null`
- **Obrigatório não nulo**: `Sim`

#### `Frequency`
- **Tipo**: `float`
- **Descrição**: Controla o nível de detalhe do terreno no bioma. Frequências maiores criam terrenos mais rugosos.
- **Valor Padrão**: `null`
- **Obrigatório não nulo**: `Sim`

#### `Persistence`
- **Tipo**: `float` (0 a 1)
- **Descrição**: Define como a influência de cada "octave" diminui conforme mais camadas de ruído são adicionadas.
- **Valor Padrão**: `1`
- **Obrigatório não nulo**: `Sim`

#### `Objects`
- **Tipo**: `List<BiomeObject>`
- **Descrição**: Lista de objetos que podem ser gerados neste bioma (ex.: árvores, rochas).
- **Valor Padrão**: `null`
- **Obrigatório não nulo**: `Não`

#### `MaxNumberOfObjects`
- **Tipo**: `float`
- **Descrição**: Número máximo de objetos que podem ser gerados dentro deste bioma.
- **Valor Padrão**: `null`
- **Obrigatório não nulo**: `Sim` *depende do que foi configurado*

#### `MobList`
- **Tipo**: `List<SpawnableMob>`
- **Descrição**: Lista de mobs que podem ser gerados neste bioma.
- **Valor Padrão**: `null`
- **Obrigatório não nulo**: `Não`

---

## 🪨 **Classe `BiomeObject`**

Representa um objeto que pode ser gerado em um bioma específico.

### **Campos da Classe `BiomeObject`**

#### `TerrainObject`
- **Tipo**: `GameObject`
- **Descrição**: O objeto do Unity a ser instanciado no terreno (ex.: árvore, pedra).
- **Valor Padrão**: `null`
- **Obrigatório não nulo**: `Sim`

#### `Weight`
- **Tipo**: `float`
- **Descrição**: Peso relativo do objeto ao escolher entre outros possíveis para o mesmo bioma.
- **Valor Padrão**: `null`
- **Obrigatório não nulo**: `Não` *depende do que foi configurado*

#### `ProbabilityToSpawn`
- **Tipo**: `float`
- **Descrição**: Probabilidade de um objeto ser gerado em um ponto válido.
- **Valor Padrão**: `null`
- **Obrigatório não nulo**: `Sim`

#### `CurrentNumberOfThisObject`
- **Tipo**: `int`
- **Descrição**: Número atual de instâncias deste objeto no bioma.
- **Valor Padrão**: `0`
- **Obrigatório não nulo**: `Sim` *depende do que foi configurado*

#### `MaxNumberOfThisObject`
- **Tipo**: `int`
- **Descrição**: Número máximo de instâncias deste objeto permitido no bioma.
- **Valor Padrão**: `null`
- **Obrigatório não nulo**: `Sim` *depende do que foi configurado*

---
## 🎮 **MobActionsController **

Script responsável por helpers methods e definir dados de cada mob


### `statusController`
- **Tipo**: `MobStatusController`
- **Descrição**: Referência ao controlador de status do mob, responsável por gerenciar os atributos e condições do mob, como saúde e energia.
- **Valor Padrão**: `null`
- **Obrigatório não nulo**: `Sim`

### `wanderDistance`
- **Tipo**: `float`
- **Descrição**: Define a distância máxima que o mob pode percorrer em uma única movimentação enquanto estiver se deslocando aleatoriamente.
- **Valor Padrão**: `50f`
- **Obrigatório não nulo**: `Sim`

### `maxWalkTime`
- **Tipo**: `float`
- **Descrição**: Define o tempo máximo que o mob pode andar antes de precisar parar para descansar.
- **Valor Padrão**: `6f`
- **Obrigatório não nulo**: `Sim`

### `patrolPoints`
- **Tipo**: `Vector3[]`
- **Descrição**: Pontos de patrulha que o mob seguirá durante o comportamento de patrulha (caso não queria que ele patrulha não preencha).
- **Valor Padrão**: `null`
- **Obrigatório não nulo**: `Sim`  *depende do que foi configurado exp: se deseja que ele não patrulhe, deixe como null*

### `currentPatrolPoint`
- **Tipo**: `int`
- **Descrição**: Índice do ponto de patrulha atual que o mob está se dirigindo.
- **Valor Padrão**: `0`
- **Obrigatório não nulo**: `Sim` 

### `idleTime`
- **Tipo**: `float`
- **Descrição**: O tempo que o mob ficará em estado de inatividade antes de começar a se mover novamente.
- **Valor Padrão**: `5f`
- **Obrigatório não nulo**: `Sim` 

### `type`
- **Tipo**: `MobType`
- **Descrição**: O tipo do mob o que o determina seus tipos !!!Importante no Enum de MobType ter um player ou então ele nunca detectará um player caso você queira deixar esse mob como predador de Players.
- **Valor Padrão**: `null`
- **Obrigatório não nulo**: `Sim` 

### `detectionRange`
- **Tipo**: `float`
- **Descrição**: Distância máxima dentro da qual o mob pode detectar presas.
- **Valor Padrão**: `10f`
- **Obrigatório não nulo**: `Sim` 

### `detectionCast`
- **Tipo**: `Cast`
- **Descrição**: Objeto responsável pela lógica de detecção de predadores ou presas, utilizando um feixe de detecção (cast) para verificar a área ao redor do mob.
- **Valor Padrão**: `null`
- **Obrigatório não nulo**: `Sim`

### `escapeMaxDistance`
- **Tipo**: `float`
- **Descrição**: A distância máxima que uma presa pode percorrer para escapar de um predador.
- **Valor Padrão**: `80f`
- **Obrigatório não nulo**: `Sim`  *depende do que foi configurado*

### `currentPredator`
- **Tipo**: `MobActionsController`
- **Descrição**: Referência ao predador atual que está perseguindo a presa(este MOB).
- **Valor Padrão**: `null` *recomendado não preencher*
- **Obrigatório não nulo**: `Não`  

### `maxChaseTime`
- **Tipo**: `float`
- **Descrição**: O tempo máximo que o predador irá perseguir a presa antes de desistir (só será utilziado se presas forem definidas).
- **Valor Padrão**: `10f`
- **Obrigatório não nulo**: `Sim` 

### `biteDamage`
- **Tipo**: `int`
- **Descrição**: O dano causado pelo predador quando captura a presa (só será utilziado se presas forem definidas).
- **Valor Padrão**: `3`
- **Obrigatório não nulo**: `Sim` 

### `isPartialWait`
- **Tipo**: `bool`
- **Descrição**: Se o predador deve parar após morder ou continuar perseguindo e atacar novamente.
- **Valor Padrão**: `false`
- **Obrigatório não nulo**: `Sim` 

### `biteCooldown`
- **Tipo**: `float`
- **Descrição**: O tempo de recarga entre mordidas consecutivas do predador.
- **Valor Padrão**: `1f`
- **Obrigatório não nulo**: `Sim` 

### `attackDistance`
- **Tipo**: `float`
- **Descrição**: Distância máxima à qual o predador pode atacar a presa.
- **Valor Padrão**: `2f`
- **Obrigatório não nulo**: `Sim` 

### `currentChaseTarget`
- **Tipo**: `MobActionsController`
- **Descrição**: Referência ao alvo atual que o predador está perseguindo.
- **Valor Padrão**: `null`
- **Obrigatório não nulo**: `Não` 

### `playerHasMaxChaseTime`
- **Tipo**: `bool`
- **Descrição**: Se o predador pode ou não perseguir o jogador por um tempo limitado.
- **Valor Padrão**: `false`
- **Obrigatório não nulo**: `Sim` 

### `currentPlayerTarget`
- **Tipo**: `PlayerStatusController`
- **Descrição**: Referência ao jogador que o predador está perseguindo.
- **Valor Padrão**: `null`
- **Obrigatório não nulo**: `Não` 

### `Preys`
- **Tipo**: `List<MobType>`
- **Descrição**: Lista de tipos de mobs que o predador considera como presa.
- **Valor Padrão**: `null`
- **Obrigatório não nulo**: `Não` 

### `stoppingMargin`
- **Tipo**: `float`
- **Descrição**: Margem de distância onde o mob começará a parar de se mover em direção a um destino (é importante pois com base nas características físicas do MOB pode acabar não verificando que chego ao destino por um bloqueio físico do próprio mob já que o centro pode está longe de mais).
- **Valor Padrão**: `0f`
- **Obrigatório não nulo**: `Sim` 

---
## 🔮 **Cast**

### `castType`
- **Tipo**: `CastType`
- **Descrição**: Tipo de cast a ser utilizado. Pode ser uma esfera, caixa, cápsula ou raio.
- **Valor Padrão**: `CastType.Sphere`
- **Obrigatório não nulo**: `Sim` 

### `castSize`
- **Tipo**: `float`
- **Descrição**: Tamanho do cast. Esse valor define o raio de uma esfera ou cápsula, o comprimento de um raio ou o tamanho de uma caixa.
- **Valor Padrão**: `5f`
- **Obrigatório não nulo**: `Sim` 

### `boxSize`
- **Tipo**: `Vector3`
- **Descrição**: Tamanho da caixa no caso de `CastType.Box`. Define as dimensões da caixa para o cast.
- **Valor Padrão**: `null`
- **Obrigatório não nulo**: `Não` 

### `customOrigin`
- **Tipo**: `Vector3`
- **Descrição**: Posição de origem customizada para o cast. Define o ponto de origem para o cálculo do cast.
- **Valor Padrão**: `Vector3.zero`
- **Obrigatório não nulo**: `Sim` 

### `customAngle`
- **Tipo**: `Vector3`
- **Descrição**: Ângulo de rotação customizado para o cast. Usado para rotação de caixa e outros tipos de cast.
- **Valor Padrão**: `Vector3.zero`
- **Obrigatório não nulo**: `Sim` 

### `targetLayers`
- **Tipo**: `LayerMask`
- **Descrição**: Máscara de camada que define quais camadas de objetos devem ser detectadas pelo cast.
- **Valor Padrão**: `null`
- **Obrigatório não nulo**: `Sim` 

---

## 🛠️ **Contribuição**

1. Faça um fork do repositório.
2. Crie um branch para sua funcionalidade (`git checkout -b minha-funcionalidade`).
3. Faça commit das mudanças (`git commit -m 'Adiciona nova funcionalidade'`).
4. Envie o branch (`git push origin minha-funcionalidade`).
5. Abra um Pull Request!

---

## 📜 **Licença**

Este projeto está licenciado sob a licença MIT. Veja o arquivo [LICENSE](./LICENSE) para mais detalhes.
