// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * Result of boolean conversion from nonempty string value (length is not zero) is true; from empty String (length is zero) is false
 *
 * @path ch09/9.2/S9.2_A5_T4.js
 * @description Any nonempty string convert to Boolean by implicit transformation
 */

// CHECK#1
if (!(" ") !== false) {
  $ERROR('#1: !(" ") === false. Actual: ' + (!(" ")));	
}

// CHECK#2
if (!("Nonempty String") !== false) {
  $ERROR('#2: !("Nonempty String") === false. Actual: ' + (!("Nonempty String")));	
}

