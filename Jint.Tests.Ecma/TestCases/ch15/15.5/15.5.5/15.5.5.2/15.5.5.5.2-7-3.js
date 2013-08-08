/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * 15.5.5.2 defines [[GetOwnProperty]] for Strings. It supports using indexing
 * notation to look up non numeric property names.
 *
 * @path ch15/15.5/15.5.5/15.5.5.2/15.5.5.5.2-7-3.js
 * @description String object indexing returns undefined if the numeric index is greater than the string length
 */


function testcase() {
  var s = new String("hello world");

  if (s[11] === undefined) {
    return true;
  }
 }
runTestCase(testcase);
