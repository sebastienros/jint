/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * 15.5.5.2 defines [[GetOwnProperty]] for Strings. It supports using indexing
 * notation to look up non numeric property names.
 *
 * @path ch15/15.5/15.5.5/15.5.5.2/15.5.5.5.2-1-1.js
 * @description String object supports bracket notation to lookup of data properties
 */


function testcase() {
  var s = new String("hello world");
  s.foo = 1;
  
  if (s["foo"] === 1) {
    return true;
  }
 }
runTestCase(testcase);
