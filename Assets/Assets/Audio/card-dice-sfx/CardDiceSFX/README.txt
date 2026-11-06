Card & Dice SFX
by spasskuchen64

268 one-shot sound effects for card, board and tabletop games, 134 seconds of audio.
WAV, 44.1 kHz, 16 bit, mono. Peak at or below -1 dBFS, no silence at either end.

FOLDERS
  card_flip/  16 files: 8 crisp, 8 soft
  card_slide/  20 files: 7 table, 7 felt, 6 fan
  card_deal/  22 files: 8 wood, 8 felt, 6 burst
  card_shuffle/  20 files: 6 riffle, 5 bridge, 4 overhand, 5 cut
  card_place/  22 files: 8 table, 8 felt, 6 slap
  dice_roll/  23 files: 9 wood, 9 felt, 5 cup
  dice_hit/  22 files: 8 wood, 8 felt, 6 clack
  chips/  28 files: 8 place, 8 stack, 6 bet, 6 riffle
  token/  32 files: 7 wood, 7 plastic, 6 metal, 6 slide, 6 hop
  coin/  23 files: 8 drop, 6 flip, 3 spin, 6 pile
  paper/  18 files: 8 page, 6 rustle, 4 pencil
  stinger/  22 files: 6 win_small, 5 win_big, 6 lose_small, 5 lose_big
  sounds.csv   every file with category, length (seconds) and peak (dBFS)
  LICENSE.txt  what you may and may not do

Files are named category_kind_take, for example dice_roll_felt_04.wav.

Loudness is the same for all takes of one kind, so you can pick any take at random.
Felt sets sit lower than wood sets on purpose, and stingers sit a little above the rest.

PICKING AT RANDOM
Do not call pure random: it repeats the last take often enough for players to notice.
Use a shuffle bag: play every take once in random order, reshuffle, and do not
start the new round with the take that just played. On top of that, change each
play slightly: pitch by +-3 percent and volume by up to -2 dB.

GODOT 4
1. Copy the folders into your project, for example res://audio/table/.
2. Create an AudioStreamRandomizer, add all card_flip_crisp files as streams.
3. Set playback_mode to Random (Avoid Repetition), random_pitch to 1.03
   and random_volume_offset_db to 2.
4. Assign it to an AudioStreamPlayer and call play() when the card turns.
   Godot imports WAV with loop off, which is what you want here.

UNITY
1. Drop the folders into Assets/Audio/.
2. For short sounds use Load Type "Decompress On Load", Compression "PCM" or "ADPCM".
3. Keep an AudioClip[] per kind and use this shuffle bag:

   using UnityEngine;
   public class ShuffleBag {
       readonly AudioClip[] c; int i; AudioClip last;
       public ShuffleBag(AudioClip[] clips) { c = clips; i = c.Length; }
       public AudioClip Next() {
           if (i >= c.Length) {
               for (int k = c.Length - 1; k > 0; k--) { int j = Random.Range(0, k + 1); (c[k], c[j]) = (c[j], c[k]); }
               if (c.Length > 1 && c[0] == last) (c[0], c[1]) = (c[1], c[0]);
               i = 0;
           }
           return last = c[i++];
       }
   }

   source.pitch = Random.Range(0.97f, 1.03f);
   source.PlayOneShot(bag.Next(), Random.Range(0.8f, 1f));

TIMING TIPS
Most hits start at the very start of the file. Flips, deals and page turns have a
short movement before the landing, about 60 to 120 ms: start them when the motion starts,
not when the card touches the table. Dealing several cards: trigger one card_deal file per
card, 100 to 150 ms apart. dice_roll files already hold 1 to 5 dice, so use a single file
per throw. To match the table in your game, stay on one surface: felt or wood.

HOW THESE WERE MADE
Every sound is synthesized by Node.js scripts from models of what physically happens: contact
force, resonances of cards, dice, chips and table, friction, air. There are no recordings and no
AI audio generator. The scripts were written with AI help (Claude).
Quality was checked by measurement only: no human has listened to these files before release.
See LICENSE.txt for the terms.
