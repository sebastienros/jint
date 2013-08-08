// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * RegExp.prototype.test has not prototype property
 *
 * @path ch15/15.10/15.10.6/15.10.6.3/S15.10.6.3_A6.js
 * @description Checking RegExp.prototype.test.prototype
 */

//CHECK#1
if (RegExp.prototype.test.prototype !== undefined) {
  $ERROR('#1: RegExp.prototype.test.prototype === undefined. Actual: ' + (RegExp.prototype.test.prototype));
}


