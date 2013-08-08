// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * RegExp instance has not [[construct]] property
 *
 * @path ch15/15.10/15.10.7/S15.10.7_A2_T2.js
 * @description Checking if creating "new RegExp" instance fails
 */

//CHECK#1
try {
  $ERROR('#1.1: new new RegExp throw TypeError. Actual: ' + (new new RegExp));
} catch (e) {
  if ((e instanceof TypeError) !== true) {
    $ERROR('#1.2: new new RegExp throw TypeError. Actual: ' + (e));
  }
}


