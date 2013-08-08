/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-6.js
 * @description Array.prototype.indexOf must return correct index(null)
 */


function testcase() {
  var obj = {toString:function (){return null}};
  var _null = null;
  var a = new Array(true,undefined,0,false,_null,1,"str",0,1,obj,true,false,null);
  if (a.indexOf(null) === 4 )  //a[4]=_null
  {
    return true;
  }
 }
runTestCase(testcase);
