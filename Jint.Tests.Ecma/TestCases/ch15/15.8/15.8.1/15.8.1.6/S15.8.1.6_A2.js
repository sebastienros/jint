// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * Value Property PI of the Math Object has the attribute DontEnum
 *
 * @path ch15/15.8/15.8.1/15.8.1.6/S15.8.1.6_A2.js
 * @description Checking if Math.PI property has the attribute DontEnum
 */

// CHECK#1
for(x in Math) {
  if(x === "PI") {
    $ERROR('#1: Value Property PI of the Math Object hasn\'t attribute DontEnum: \'for(x in Math) {x==="PI"}\'');
  }
}


