# Mqtt.Client
a Mqtt Client based on Dotnetty mainly translated from https://github.com/jetlinks/netty-mqtt-client.

## Example 1:Pub/Sub same Topic

    Mqtt.Client.MqttClient mqttClient = new Mqtt.Client.MqttClient();
    var result = await mqttClient.ConnectAsync("127.0.0.1", 1883);
    Console.WriteLine($"connect {result.Success}");
    if (result.Success)
    {
        await mqttClient.SubscribeAsync("test/1", DotNetty.Codecs.Mqtt.Packets.QualityOfService.AtLeastOnce, (packet) =>
        {
            var pubpacket = (DotNetty.Codecs.Mqtt.Packets.PublishPacket)packet;
            Console.WriteLine(pubpacket.Payload.GetString(0, pubpacket.Payload.WriterIndex, UTF8Encoding.UTF8));
        });
        await Task.Delay(1000);
        for(int i = 0; i < 8; i++)
        {
            await mqttClient.PublishAsync("test/1", UTF8Encoding.UTF8.GetBytes(i.ToString()));
            await Task.Delay(1000);
        }
        await Task.Delay(1000);
        await mqttClient.DisconnectAsync();
          

## Example 2:HeartBeat

    Mqtt.Client.HeartbeatMqttClient mqttClient = new Mqtt.Client.HeartbeatMqttClient();
    var result = await mqttClient.ConnectAsync("127.0.0.1", 1883);
    Console.WriteLine($"connect {result.Success}");
    if (result.Success)
    {
        await Task.Delay(100000);
        await mqttClient.DisconnectAsync();
    }
    
## License
MIT License
