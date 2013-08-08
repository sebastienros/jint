/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.10/12.10-2-1.js
 * @description with - expression being Number
 */


function testcase() {
  var o = 2;
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
