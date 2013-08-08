/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-0-3.js
 * @description Object.getPrototypeOf must take 1 parameter
 */


function testcase() {
  try
  {
    Object.getPrototypeOf();
  }
  catch(e)
  {
    if(e instanceof TypeError)
      return true;
  }
 }
runTestCase(testcase);
