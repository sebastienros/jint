// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * The initial value of Error.prototype.message is ""
 *
 * @path ch15/15.11/15.11.4/S15.11.4.3_A2.js
 * @description Checking value of Error.prototype.message
 */

//////////////////////////////////////////////////////////////////////////////
// CHECK#1
if (typeof Error.prototype.message !== "string") {
  $ERROR('#1: typeof Error.prototype.message === "string". Actual: ' + Error.prototype.message);
}
//
//////////////////////////////////////////////////////////////////////////////

