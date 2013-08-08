/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * Function.constructor
 * Function.prototype
 * Array.prototype
 * String.prototype
 * Boolean.prototype
 * Number.prototype
 * Date.prototype
 * RegExp.prototype
 * Error.prototype
 *
 * @path ch15/15.2/15.2.3/15.2.3.13/15.2.3.13-2-21.js
 * @description Object.isExtensible returns true for all built-in objects (Error.prototype)
 */


function testcase() {
  var e = Object.isExtensible(Error.prototype);
  if (e === true) {
    return true;
  }
 }
runTestCase(testcase);
