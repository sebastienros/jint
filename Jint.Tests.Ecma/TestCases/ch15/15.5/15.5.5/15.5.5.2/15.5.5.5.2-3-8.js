/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * 15.5.5.2 defines [[GetOwnProperty]] for Strings. It supports using indexing
 * notation to look up non numeric property names.
 *
 * @path ch15/15.5/15.5.5/15.5.5.2/15.5.5.5.2-3-8.js
 * @description String value indexing returns undefined if the numeric index ( >= 2^32-1) is not an array index
 */


function testcase() {
  var s = String("hello world");

  if (s[Math.pow(2, 32)-1]===undefined) {
    return true;
  }
 }
runTestCase(testcase);
