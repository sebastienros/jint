// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * The String prototype object is itself a String object whose value is an empty string
 *
 * @path ch15/15.5/15.5.4/S15.5.4_A2.js
 * @description Checking String.prototype
 */

//////////////////////////////////////////////////////////////////////////////
//CHECK#1
if (String.prototype !="") {
  $ERROR('#1: String.prototype =="". Actual: String.prototype =='+String.prototype ); 
}
//
//////////////////////////////////////////////////////////////////////////////

