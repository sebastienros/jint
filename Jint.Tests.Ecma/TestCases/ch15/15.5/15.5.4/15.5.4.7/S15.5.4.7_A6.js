// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * String.prototype.indexOf has not prototype property
 *
 * @path ch15/15.5/15.5.4/15.5.4.7/S15.5.4.7_A6.js
 * @description Checking String.prototype.indexOf.prototype
 */

//////////////////////////////////////////////////////////////////////////////
//CHECK#1
if (String.prototype.indexOf.prototype !== undefined) {
  $ERROR('#1: String.prototype.indexOf.prototype === undefined. Actual: '+String.prototype.indexOf.prototype ); 
}
//
//////////////////////////////////////////////////////////////////////////////

