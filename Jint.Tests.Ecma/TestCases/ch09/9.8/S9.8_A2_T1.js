// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * Result of ToString conversion from null value is "null"
 *
 * @path ch09/9.8/S9.8_A2_T1.js
 * @description null convert to String by explicit transformation
 */

// CHECK#1
if (String(null) !== "null") {
  $ERROR('#1: String(null) === "null". Actual: ' + (String(null))); 
} 

