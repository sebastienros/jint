/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.10/12.10-2-3.js
 * @description with - expression being string
 */


function testcase() {
  var o = "str";
  var foo = 1;
  try
  {
    with (o) {
      foo = 42;
    }
  }
  catch(e)
  {
  }
  return true;
  
 }
runTestCase(testcase);
