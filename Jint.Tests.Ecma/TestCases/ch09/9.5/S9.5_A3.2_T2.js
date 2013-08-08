// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * Operator uses floor, abs
 *
 * @path ch09/9.5/S9.5_A3.2_T2.js
 * @description Use operator ~
 */

// CHECK#1
if (~1.2345 !== ~1) {
  $ERROR('#1: ~1.2345 === ~1)');
}

// CHECK#2
if (~-5.4321 !== ~-5) {
  $ERROR('#2: ~-5.4321 === ~-5)');
}

