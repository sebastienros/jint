// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * Result of boolean conversion from null value is false
 *
 * @path ch09/9.2/S9.2_A2_T2.js
 * @description null convert to Boolean by implicit transformation
 */

// CHECK#1
if (!(null) !== true) {
  $ERROR('#1: !(null) === true. Actual: ' + (!(null))); 
}

