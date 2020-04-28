import { Component, ViewChild, ElementRef, OnInit } from "@angular/core";
import { HubConnection } from "@aspnet/signalr";
import * as signalR from "@aspnet/signalr";
declare const google: any;

@Component({
  selector: "app-root",
  templateUrl: "./app.component.html",
  styleUrls: ["./app.component.css"],
})
export class AppComponent implements OnInit {
  private hubConnection: HubConnection;

  private bindConnectionMessage(connection) {
    var messageCallback = function (location: {
      Address: string;
      Lat: string;
      Lng: string;
    }, message) {
      if (!location) return;
      const newCoordinates = new google.maps.LatLng(location.Lat, location.Lng);
      const marker = new google.maps.Marker({
        position: newCoordinates,
        map: this.map,
        animation: google.maps.Animation.DROP
      });
      marker.setMap(this.map);
    };
    // Create a function that the hub can call to broadcast messages.
    connection.on("broadcastMessage", messageCallback.bind(this));
    connection.on("echo", messageCallback.bind(this));
    connection.onclose(this.onConnectionError.bind(this));
  }

  private onConnectionError(error) {
    if (error && error.message) {
      console.error(error.message);
    }
  }

  ngOnInit(): void {
    var connection = new signalR.HubConnectionBuilder()
      .withUrl("/center")
      .build();

    this.bindConnectionMessage(connection);
    connection
      .start()
      .then(function () {
        console.log("connection started");
      })
      .catch(function (error) {
        console.error(error.message);
      });
  }

  @ViewChild("mapContainer", { static: true }) gmap: ElementRef;
  map: google.maps.Map;
  lat = 40.73061;
  lng = -73.935242;

  coordinates = new google.maps.LatLng(this.lat, this.lng);

  mapOptions: google.maps.MapOptions = {
    center: this.coordinates,
    zoom: 4,
  };

  ngAfterViewInit() {
    this.mapInitializer();
  }

  mapInitializer() {
    this.map = new google.maps.Map(this.gmap.nativeElement, this.mapOptions);
  }
}
