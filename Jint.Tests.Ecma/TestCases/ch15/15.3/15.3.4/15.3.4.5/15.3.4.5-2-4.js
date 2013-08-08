/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * 15.3.4.5 step 2 specifies that a TypeError must be thrown if the Target is not callable.
 *
 * @path ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-2-4.js
 * @description Function.prototype.bind allows Target to be a constructor (String)
 */


function testcase() {
  var bsc = String.bind(null);
  var s = bsc("hello world");
  if (s === "hello world") {
    return true;
  }
 }
runTestCase(testcase);
