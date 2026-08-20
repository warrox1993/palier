export function mifflinStJeor(poidsKg: number, tailleCm: number, age: number): number {
  return 10 * poidsKg + 6.25 * tailleCm - 5 * age + 5
}
