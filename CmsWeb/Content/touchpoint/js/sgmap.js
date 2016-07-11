$(function () {
    initializeMaps();
});
function initializeMaps() {
    var latlng = new google.maps.LatLng(-33.92, 151.25);
    var myOptions = {
        zoom: 10,
        center: latlng,
        mapTypeId: google.maps.MapTypeId.ROADMAP,
        mapTypeControl: false
    };
    var bounds = new google.maps.LatLngBounds();
    var map = new google.maps.Map(document.getElementById("map"), myOptions);
    var marker, i;
    for (i = 0; i < markers.length; i++) {
        marker = new google.maps.Marker({
            position: new google.maps.LatLng(markers[i].latitude, markers[i].longitude),
            map: map,
            icon: markers[i].image
        });

        bounds.extend(marker.position);
        google.maps.event.addListener(marker, 'click', (function (marker, i) {
            return function () {
                var infowindow = new google.maps.InfoWindow({ content: markers[i].html });
                infowindow.open(map, marker);
            };
        })(marker, i));
    }
    map.fitBounds(bounds);

    google.maps.event.addListenerOnce(map, 'idle', function(){
        var center = new google.maps.LatLng(40.757577, -73.969246);
        map.setCenter(center);
        map.setZoom(12);
    });

    window.map = map;
}
