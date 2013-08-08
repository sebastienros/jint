/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * This test should be run without any built-ins being added/augmented.
 * The name JSON must be bound to an object.
 * 4.2 calls out JSON as one of the built-in objects.
 *
 * @path ch15/15.12/15.12-0-1.js
 * @description JSON must be a built-in object
 */


function testcase() {
  var o = JSON;
  if (typeof(o) === "object") {  
    return true;
  }
 }
runTestCase(testcase);
