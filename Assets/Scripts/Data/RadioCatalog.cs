using System.Collections.Generic;
using UnityEngine;

namespace SanMonica.Data
{
    /// <summary>
    /// Eight original San Monica radio stations. All music is synthesised at
    /// runtime from these parameters, so nothing is sampled or licensed - the
    /// soundtrack is generated procedurally and is unique to this project.
    /// </summary>
    public static class RadioCatalogData
    {
        private static List<RadioStationDefinition> _all;
        public static List<RadioStationDefinition> All { get { if (_all == null) Build(); return _all; } }

        private static void Build()
        {
            _all = new List<RadioStationDefinition>
            {
                new RadioStationDefinition {
                    id="vireo-fm", displayName="Vireo FM", genre="Synthwave", dj="Nadia Frost",
                    rootNote=45, bpm=112f, energy=0.7f, distortion=0.15f,
                    accent=new Color(0.85f,0.25f,0.75f),
                    djLines=new[]{
                        "Vireo FM, ninety-one point one. Neon on wet asphalt, that's the whole mood tonight.",
                        "You're rolling through Vireo Heights with me, Nadia Frost. Keep both hands on the wheel.",
                        "Traffic on the Ninth is stacked from Foundry to the bridge. Take the surface streets."},
                    adverts=new[]{
                        "Corvale Wren. It fits in the space nobody else wanted. Corvale - small is a strategy.",
                        "Threadline. Because the camera on the corner deserves a good angle of you."},
                    newsLines=new[]{
                        "Halcyon Dynamics announced another expansion at the Redwater cargo terminal today.",
                        "SMPD reports a third night of increased patrols along the Iron Bay waterfront."}
                },
                new RadioStationDefinition {
                    id="iron-bay", displayName="Iron Bay Rock", genre="Garage Rock", dj="Cutter Vane",
                    rootNote=40, bpm=138f, energy=0.9f, distortion=0.75f,
                    accent=new Color(0.85f,0.35f,0.10f),
                    djLines=new[]{
                        "Iron Bay Rock. If your windows aren't rattling, turn it up.",
                        "Cutter Vane, still broadcasting from a shipping container. Still louder than you."},
                    adverts=new[]{
                        "Steadman Brawler four-forty. Two seats. No apologies.",
                        "Foundry Supply. Everything you need, nothing you can return."},
                    newsLines=new[]{ "Dock strike talks collapsed again this morning. Nobody is surprised." }
                },
                new RadioStationDefinition {
                    id="cumbia-costa", displayName="Cumbia Costa", genre="Cumbia", dj="Lucia Bermejo",
                    rootNote=48, bpm=96f, energy=0.65f, distortion=0.05f,
                    accent=new Color(0.95f,0.65f,0.15f),
                    djLines=new[]{
                        "Cumbia Costa, straight out of the Marigold Quarter. Move something.",
                        "Lucia Bermejo with you until the sun comes up over Palmetto."},
                    adverts=new[]{ "Marigold Taqueria. Open till two. Cash only. You know why." },
                    newsLines=new[]{ "Marigold street market extends hours through the weekend festival." }
                },
                new RadioStationDefinition {
                    id="frequency-7", displayName="Frequency 7", genre="Techno", dj="AUTO-7",
                    rootNote=38, bpm=142f, energy=1f, distortion=0.35f,
                    accent=new Color(0.20f,0.85f,0.90f),
                    djLines=new[]{
                        "FREQUENCY SEVEN. NO HOST. NO REQUESTS. ONLY SIGNAL.",
                        "Transmission continues. Ignore the sirens."},
                    adverts=new[]{ "Static Room. Downtown. Doors at twenty-one hundred." },
                    newsLines=new[]{ "Power fluctuations reported across the Foundry Flats grid." }
                },
                new RadioStationDefinition {
                    id="static-gold", displayName="Static Gold", genre="Lo-fi Jazz", dj="Marlon Teak",
                    rootNote=44, bpm=78f, energy=0.35f, distortion=0.08f,
                    accent=new Color(0.80f,0.70f,0.40f),
                    djLines=new[]{
                        "Static Gold. Slow down. The city will still be there.",
                        "Marlon Teak, three in the morning, every night. Somebody has to be."},
                    adverts=new[]{ "Blue Heron Diner. Coffee that has seen things." },
                    newsLines=new[]{ "Fog advisory in effect along the Palmetto shoreline until dawn." }
                },
                new RadioStationDefinition {
                    id="redline", displayName="Redline 88", genre="Punk", dj="Sena Marrow",
                    rootNote=41, bpm=168f, energy=1f, distortion=0.85f,
                    accent=new Color(0.90f,0.15f,0.30f),
                    djLines=new[]{
                        "Redline eighty-eight. We got kicked off two towers this year. Third time's charm.",
                        "Sena Marrow. If the SMPD is listening: hello, and no."},
                    adverts=new[]{ "Coastline Arms. Paperwork optional in some counties. Not this one." },
                    newsLines=new[]{ "Protest outside Halcyon Tower entered its fourth day." }
                },
                new RadioStationDefinition {
                    id="sunday-drive", displayName="Sunday Drive", genre="Soul", dj="Everett Pace",
                    rootNote=46, bpm=88f, energy=0.5f, distortion=0.05f,
                    accent=new Color(0.75f,0.45f,0.25f),
                    djLines=new[]{
                        "Sunday Drive. Windows down, Crestwood on the left, ocean on the right.",
                        "Everett Pace here. Take the long way home."},
                    adverts=new[]{ "Crestwood Atelier. If you have to ask, keep driving." },
                    newsLines=new[]{ "Clear skies expected over Halcyon Bay through the weekend." }
                },
                new RadioStationDefinition {
                    id="ksmo", displayName="KSMO Talk", genre="Talk", dj="Priya Vance", talkOnly=true,
                    rootNote=43, bpm=70f, energy=0.2f, distortion=0f,
                    accent=new Color(0.55f,0.60f,0.70f),
                    djLines=new[]{
                        "This is KSMO. Priya Vance. Let's talk about who actually owns the waterfront.",
                        "Caller says the SMPD helicopter woke her twice last night. Caller is correct.",
                        "We invited Halcyon Dynamics to comment. They sent a logo."},
                    adverts=new[]{
                        "Rook's Garage. Honest work on dishonest cars.",
                        "Cinder Fuel. Twenty-four hours, because so is your week."},
                    newsLines=new[]{
                        "City council delayed the Iron Bay redevelopment vote for a fourth time.",
                        "Kestrel University announced a new marine research grant this morning.",
                        "SMPD confirmed an ongoing investigation into vehicle theft rings in Foundry Flats."}
                },
            };
        }
    }
}
