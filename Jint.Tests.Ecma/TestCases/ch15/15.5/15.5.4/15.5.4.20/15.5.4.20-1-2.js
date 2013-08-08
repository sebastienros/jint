/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-1-2.js
 * @description String.prototype.trim throws TypeError when string is null
 */


function testcase() {
  try
  {
    String.prototype.trim.call(null);  
    return false;
  }
  catch(e)
  {
    return e instanceof TypeError;
  }
 }
runTestCase(testcase);
