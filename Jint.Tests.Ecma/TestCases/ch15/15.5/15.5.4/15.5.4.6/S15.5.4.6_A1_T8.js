// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * String.prototype.concat([,[...]])
 *
 * @path ch15/15.5/15.5.4/15.5.4.6/S15.5.4.6_A1_T8.js
 * @description Call concat([,[...]]) function with void 0 argument of string object
 */

//////////////////////////////////////////////////////////////////////////////
//CHECK#1
//since ToString(void 0) evaluates to "undefined" concat(void 0) evaluates to concat("undefined")
if (String(42).concat(void 0) !== "42undefined") {
  $ERROR('#1: String(42).concat(void 0) === "42undefined". Actual: '+String(42).concat(void 0) ); 
}
//
//////////////////////////////////////////////////////////////////////////////

