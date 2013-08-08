// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * Array prototype object has length property whose value is +0
 *
 * @path ch15/15.4/15.4.4/S15.4.4_A1.3_T1.js
 * @description Array.prototype.length === 0
 */

//CHECK#1
if (Array.prototype.length !== 0) {
  $ERROR('#1.1: Array.prototype.length === 0. Actual: ' + (Array.prototype.length));
} else {
  if (1 / Array.prototype.length !== Number.POSITIVE_INFINITY) {
    $ERROR('#1.2: Array.prototype.length === +0. Actual: ' + (Array.prototype.length));
  }
} 
   

