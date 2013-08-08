/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-1-1.js
 * @description String.prototype.trim throws TypeError when string is undefined
 */


function testcase() {
  try
  {
    String.prototype.trim.call(undefined); 
    return false; 
  }
  catch(e)
  {
    return e instanceof TypeError;
  }
 }
runTestCase(testcase);
